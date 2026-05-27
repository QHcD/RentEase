using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;
using PropertyLeasing.MVC.Services;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

[Authorize]
public class LeaseApplicationsController : Controller
{
    private readonly PropertyLeasingDbContext          _db;
    private readonly UserManager<AppUser>              _userManager;
    private readonly NotificationService               _notifier;
    private readonly EmailService                      _emailService;
    private readonly LeaseApplicationDocumentService   _documents;

    public LeaseApplicationsController(
        PropertyLeasingDbContext db,
        UserManager<AppUser> userManager,
        NotificationService notifier,
        EmailService emailService,
        LeaseApplicationDocumentService documents)
    {
        _db           = db;
        _userManager  = userManager;
        _notifier     = notifier;
        _emailService = emailService;
        _documents    = documents;
    }

    private async Task ReloadApplyUnitSummaryAsync(CreateLeaseApplicationViewModel model)
    {
        var unit = await _db.Units
            .Include(u => u.Property)
            .FirstOrDefaultAsync(u => u.UnitId == model.UnitId);
        if (unit == null) return;
        model.UnitNumber   = unit.UnitNumber;
        model.PropertyName = unit.Property.Name;
        model.MonthlyRent  = unit.MonthlyRent;
    }

    private async Task<User?> GetAppUserAsync()
    {
        var identity = await _userManager.GetUserAsync(User);
        if (identity == null) return null;

        var appUser = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identity.Id)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);

        if (appUser != null)
        {
            if (appUser.IdentityUserId != identity.Id)
            {
                appUser.IdentityUserId = identity.Id;
                await _db.SaveChangesAsync();
            }

            if (TenantProfileSync.ApplyIdentityToAppUser(
                    identity.FullName,
                    identity.Email!,
                    identity.Phone,
                    v => appUser.FullName = v,
                    v => appUser.Email = v,
                    v => appUser.Phone = v,
                    new TenantProfileSync.ProfileSnapshot(appUser.FullName, appUser.Email, appUser.Phone)))
                await _db.SaveChangesAsync();
        }

        return appUser;
    }

    private async Task SyncLeaseApplicationUserFromIdentityAsync(User tenantUser)
    {
        if (string.IsNullOrWhiteSpace(tenantUser.IdentityUserId))
            return;

        var identity = await _userManager.FindByIdAsync(tenantUser.IdentityUserId);
        if (identity == null) return;

        if (TenantProfileSync.ApplyIdentityToAppUser(
                identity.FullName,
                identity.Email ?? tenantUser.Email,
                identity.Phone,
                v => tenantUser.FullName = v,
                v => tenantUser.Email = v,
                v => tenantUser.Phone = v,
                new TenantProfileSync.ProfileSnapshot(tenantUser.FullName, tenantUser.Email, tenantUser.Phone)))
            await _db.SaveChangesAsync();
    }

    // ── Cancel open pre-tenancy maintenance for a unit ────────────────────────
    // Does NOT call SaveChangesAsync — caller is responsible for saving.
    private async Task CancelPreTenancyMaintenanceAsync(int unitId, string reason, int changedByUserId)
    {
        var openRequests = await _db.MaintenanceRequests
            .Where(r => r.UnitId == unitId &&
                        r.ScheduledDate.HasValue &&
                        r.Status != "Cancelled" && r.Status != "Resolved" && r.Status != "Closed")
            .ToListAsync();

        var now = DateTime.Now;
        foreach (var req in openRequests)
        {
            var oldStatus      = req.Status;
            req.Status             = "Cancelled";
            req.CancellationReason = reason;

            _db.MaintenanceStatusHistories.Add(new MaintenanceStatusHistory
            {
                RequestId       = req.RequestId,
                OldStatus       = oldStatus,
                NewStatus       = "Cancelled",
                Notes           = reason,
                ChangedAt       = now,
                ChangedByUserId = changedByUserId
            });

            _db.MaintenanceRequestLogs.Add(new MaintenanceRequestLog
            {
                RequestId         = req.RequestId,
                Action            = "Cancelled",
                Details           = $"Pre-tenancy maintenance auto-cancelled. Reason: {reason}",
                PerformedByUserId = changedByUserId,
                PerformedAt       = now
            });
        }
    }

    // ── Auto-transition lease statuses based on date ──────────────────────────
    private async Task UpdateLeaseStatusesAsync()
    {
        var today = DateTime.Today;
        bool changed = false;

        // 1. Approved leases whose start date has arrived → Active
        var nowActive = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit)
            .Where(l => l.Status == "Approved" && l.LeaseStartDate <= today)
            .ToListAsync();

        foreach (var lease in nowActive)
        {
            lease.Status = "Active";
            _db.LeaseLogs.Add(new LeaseLog
            {
                LeaseId         = lease.LeaseId,
                Status          = "Active",
                ChangedByUserId = lease.Application.UserId,
                Notes           = "Lease automatically activated — start date reached.",
                CreatedAt       = DateTime.Now
            });
            changed = true;
        }

        // 2. Active leases with a scheduled termination date that has arrived → Terminated
        var scheduledTerminated = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit)
            .Include(l => l.Termination)
            .Where(l => l.Status == "Active" && l.TerminationId != null)
            .ToListAsync();

        foreach (var lease in scheduledTerminated)
        {
            if (lease.Termination != null && lease.Termination.TerminationDate <= today)
            {
                lease.Status = "Terminated";
                if (lease.Application.Unit != null)
                    lease.Application.Unit.AvailabilityStatus = "Available";

                _db.LeaseLogs.Add(new LeaseLog
                {
                    LeaseId         = lease.LeaseId,
                    Status          = "Terminated",
                    ChangedByUserId = lease.Application.UserId,
                    Notes           = $"Lease terminated as per scheduled termination date " +
                                      $"{lease.Termination.TerminationDate:dd MMM yyyy}.",
                    CreatedAt       = DateTime.Now
                });

                // Cancel any pending pre-tenancy maintenance for this unit
                await CancelPreTenancyMaintenanceAsync(
                    lease.Application.UnitId, "Lease Terminated", lease.Application.UserId);

                changed = true;
            }
        }

        // 3. Active leases whose end date has passed (no scheduled termination) → Renewed or Terminated
        var expired = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit)
            .Include(l => l.RenewLeaseApplication)
            .Where(l => l.Status == "Active" && l.LeaseEndDate < today && l.TerminationId == null)
            .ToListAsync();

        foreach (var lease in expired)
        {
            bool hasApprovedRenewal = lease.RenewLeaseApplication?.Status == "Approved";

            if (hasApprovedRenewal)
            {
                lease.Status = "Renewed";
                _db.LeaseLogs.Add(new LeaseLog
                {
                    LeaseId         = lease.LeaseId,
                    Status          = "Renewed",
                    ChangedByUserId = lease.Application.UserId,
                    Notes           = "Lease marked Renewed — renewal application is approved.",
                    CreatedAt       = DateTime.Now
                });

                // Cancel any pending pre-tenancy maintenance — tenant renewed, no turnover needed
                await CancelPreTenancyMaintenanceAsync(
                    lease.Application.UnitId, "Lease Renewed", lease.Application.UserId);
            }
            else
            {
                lease.Status = "Terminated";
                if (lease.Application.Unit != null)
                    lease.Application.Unit.AvailabilityStatus = "Available";

                _db.LeaseLogs.Add(new LeaseLog
                {
                    LeaseId         = lease.LeaseId,
                    Status          = "Terminated",
                    ChangedByUserId = lease.Application.UserId,
                    Notes           = "Lease terminated — end date passed without an approved renewal.",
                    CreatedAt       = DateTime.Now
                });

                // Cancel any pending pre-tenancy maintenance
                await CancelPreTenancyMaintenanceAsync(
                    lease.Application.UnitId, "Lease Terminated", lease.Application.UserId);

                // Reject any non-approved renewal application
                if (lease.RenewLeaseApplication != null &&
                    lease.RenewLeaseApplication.Status != "Rejected" &&
                    lease.RenewLeaseApplication.Status != "Canceled")
                {
                    lease.RenewLeaseApplication.Status = "Rejected";
                    _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
                    {
                        ApplicationId   = lease.RenewLeaseApplication.ApplicationId,
                        Status          = "Rejected",
                        ChangedByUserId = lease.Application.UserId,
                        CreatedAt       = DateTime.Now
                    });
                }
            }
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync();
    }

    // ── Unified Index: Applications & Leases ─────────────────────────────────
    public async Task<IActionResult> Index(
        string tab         = "applications",
        string appStatus   = "All",
        string leaseStatus = "All")
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        await UpdateLeaseStatusesAsync();

        bool isManager = appUser.Role == "PropertyManager";

        tab = tab.Equals("leases", StringComparison.OrdinalIgnoreCase) ? "leases"
            : tab.Equals("renewals", StringComparison.OrdinalIgnoreCase) ? "renewals"
            : "applications";

        // ── Load applications ─────────────────────────────────────────────
        var appQuery = _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .Include(a => a.Leases)
            .AsQueryable();

        if (!isManager)
            appQuery = appQuery.Where(a => a.UserId == appUser.UserId);

        var allApps = await appQuery.OrderByDescending(a => a.CreatedAt).ToListAsync();

        var (regularApps, renewalApps) =
            LeaseApplicationIndexPartitioner.PartitionByRenewal(allApps, a => a.ParentLeaseId);

        bool ShowOnPipelineTab(LeaseApplication a) =>
            LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs(
                a.Status,
                a.Leases.Select(l => l.Status));

        regularApps = regularApps.Where(ShowOnPipelineTab).ToList();
        renewalApps = renewalApps.Where(ShowOnPipelineTab).ToList();

        var statusKeys = ApplicationsAndLeasesViewModel.AppStatuses;

        var appCounts = LeaseApplicationIndexPartitioner.BuildStatusCounts(
            regularApps, a => a.Status, statusKeys);
        var renewalAppCounts = LeaseApplicationIndexPartitioner.BuildStatusCounts(
            renewalApps, a => a.Status, statusKeys);

        // "All" excludes terminal outcomes — avoids e.g. Rejected rows stacked under Occupied units in the default view
        appCounts["All"] = regularApps.Count(a => !LeaseApplicationSeedRules.HiddenFromLeaseApplicationAllFilter(a.Status));
        renewalAppCounts["All"] = renewalApps.Count(a => !LeaseApplicationSeedRules.HiddenFromLeaseApplicationAllFilter(a.Status));

        if (!ApplicationsAndLeasesViewModel.AppStatuses.Contains(appStatus, StringComparer.OrdinalIgnoreCase))
            appStatus = "All";

        var filteredRegular = appStatus == "All"
            ? regularApps.Where(a => !LeaseApplicationSeedRules.HiddenFromLeaseApplicationAllFilter(a.Status)).ToList()
            : regularApps.Where(a => LeaseApplicationIndexPartitioner.MatchesStatusTabFilter(a.Status, appStatus)).ToList();

        var filteredRenewals = appStatus == "All"
            ? renewalApps.Where(a => !LeaseApplicationSeedRules.HiddenFromLeaseApplicationAllFilter(a.Status)).ToList()
            : renewalApps.Where(a => LeaseApplicationIndexPartitioner.MatchesStatusTabFilter(a.Status, appStatus)).ToList();

        LeaseApplicationListViewModel MapListVm(LeaseApplication a) => new()
        {
            ApplicationId      = a.ApplicationId,
            UnitNumber         = a.Unit.UnitNumber,
            PropertyName       = a.Unit.Property.Name,
            TenantName         = a.User.FullName,
            RequestedStartDate = a.RequestedStartDate,
            RequestedEndDate   = a.RequestedEndDate,
            Status             = a.Status,
            Notes              = a.Notes,
            CreatedAt          = a.CreatedAt,
            ParentLeaseId      = a.ParentLeaseId
        };

        var appListVms = filteredRegular.Select(MapListVm).ToList();

        var appGroups = new List<UnitApplicationGroupViewModel>();
        var renewalAppGroups = new List<UnitApplicationGroupViewModel>();
        if (isManager)
        {
            appGroups = filteredRegular
                .GroupBy(a => a.UnitId)
                .Select(g => new UnitApplicationGroupViewModel
                {
                    UnitId             = g.Key,
                    UnitNumber         = g.First().Unit.UnitNumber,
                    PropertyName       = g.First().Unit.Property.Name,
                    AvailabilityStatus = g.First().Unit.AvailabilityStatus,
                    ApplicationCount   = g.Count(),
                    Applications       = g.Select(MapListVm).ToList()
                }).ToList();

            renewalAppGroups = filteredRenewals
                .GroupBy(a => a.UnitId)
                .Select(g => new UnitApplicationGroupViewModel
                {
                    UnitId             = g.Key,
                    UnitNumber         = g.First().Unit.UnitNumber,
                    PropertyName       = g.First().Unit.Property.Name,
                    AvailabilityStatus = g.First().Unit.AvailabilityStatus,
                    ApplicationCount   = g.Count(),
                    Applications       = g.Select(MapListVm).ToList()
                }).ToList();
        }

        var renewalAppListVms = filteredRenewals.Select(MapListVm).ToList();

        // ── Load leases ───────────────────────────────────────────────────
        var leaseQuery = _db.Leases
            .Include(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(l => l.Application.User)
            .Include(l => l.LeaseLogs)
                .ThenInclude(ll => ll.ChangedByUser)
            .Include(l => l.Termination)
            .Include(l => l.RenewLeaseApplication)
            .AsQueryable();

        if (!isManager)
            leaseQuery = leaseQuery.Where(l => l.Application.UserId == appUser.UserId);

        var allLeases = await leaseQuery.OrderByDescending(l => l.CreatedAt).ToListAsync();

        var leaseCounts = new Dictionary<string, int>
        {
            ["All"]            = allLeases.Count,
            ["PendingPayment"] = allLeases.Count(l => l.Status == "PendingPayment"),
            ["Approved"]       = allLeases.Count(l => l.Status == "Approved"),
            ["Active"]         = allLeases.Count(l => l.Status == "Active"),
            ["Terminated"]     = allLeases.Count(l => l.Status == "Terminated"),
            ["Renewed"]        = allLeases.Count(l => l.Status == "Renewed")
        };

        var filteredLeases = leaseStatus == "All"
            ? allLeases
            : allLeases.Where(l => l.Status == leaseStatus).ToList();

        var leaseVms = filteredLeases.Select(l => new LeaseListViewModel
        {
            LeaseId                 = l.LeaseId,
            ApplicationId           = l.ApplicationId,
            UnitNumber              = l.Application.Unit.UnitNumber,
            PropertyName            = l.Application.Unit.Property.Name,
            TenantName              = l.Application.User.FullName,
            LeaseStartDate          = l.LeaseStartDate,
            LeaseEndDate            = l.LeaseEndDate,
            MonthlyRent             = l.MonthlyRent,
            SecurityDeposit         = l.SecurityDeposit,
            Status                  = l.Status,
            PaymentPlanType         = l.PaymentPlanType,
            CreatedAt               = l.CreatedAt,
            TerminationId           = l.TerminationId,
            TerminationDate         = l.Termination?.TerminationDate,
            TerminationNotes        = l.Termination?.Notes,
            RenewLeaseApplicationId = l.RenewLeaseApplicationId,
            RenewApplicationStatus  = l.RenewLeaseApplication?.Status,
            Logs                    = l.LeaseLogs
                .OrderBy(ll => ll.CreatedAt)
                .Select(ll => new LeaseLogViewModel
                {
                    Status            = ll.Status,
                    ChangedByUserName = ll.ChangedByUser.FullName,
                    Notes             = ll.Notes,
                    CreatedAt         = ll.CreatedAt
                }).ToList()
        }).ToList();

        var vm = new ApplicationsAndLeasesViewModel
        {
            IsManager                = isManager,
            ActiveTab                = tab,
            AppStatusFilter          = appStatus,
            AppCounts                = appCounts,
            Applications             = appListVms,
            ApplicationGroups        = appGroups,
            RenewalApplications      = renewalAppListVms,
            RenewalApplicationGroups = renewalAppGroups,
            RenewalAppCounts         = renewalAppCounts,
            LeaseStatusFilter        = leaseStatus,
            LeaseCounts              = leaseCounts,
            Leases                   = leaseVms
        };

        return View(vm);
    }

    // ── Application Details ───────────────────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .Include(a => a.Documents)
            .Include(a => a.ApplicationLogs).ThenInclude(l => l.ChangedByUser)
            .FirstOrDefaultAsync(a => a.ApplicationId == id);

        if (application == null) return NotFound();

        if (appUser.Role == "Tenant" && application.UserId != appUser.UserId)
            return Forbid();

        await SyncLeaseApplicationUserFromIdentityAsync(application.User);

        var vm = new LeaseApplicationDetailViewModel
        {
            ApplicationId      = application.ApplicationId,
            UnitNumber         = application.Unit.UnitNumber,
            PropertyName       = application.Unit.Property.Name,
            TenantName         = application.User.FullName,
            TenantPhone        = application.User.Phone,
            TenantEmail        = application.User.Email,
            RequestedStartDate = application.RequestedStartDate,
            RequestedEndDate   = application.RequestedEndDate,
            Status             = application.Status,
            Notes              = application.Notes,
            CreatedAt          = application.CreatedAt,
            ParentLeaseId      = application.ParentLeaseId,
            Logs               = application.ApplicationLogs
                .OrderBy(l => l.CreatedAt)
                .Select(l => new LeaseApplicationLogViewModel
                {
                    Status            = l.Status,
                    ChangedByUserName = l.ChangedByUser.FullName,
                    CreatedAt         = l.CreatedAt
                }).ToList(),
            Documents = application.Documents
                .OrderBy(d => d.FileType)
                .Select(d => new ApplicationDocumentViewModel
                {
                    DocumentId      = d.DocumentId,
                    DocumentType    = d.FileType ?? "",
                    DisplayName     = LeaseApplicationDocumentRules.GetDisplayName(d.FileType ?? ""),
                    FileName        = d.FileName,
                    Status          = d.Status,
                    RejectionReason = d.RejectionReason,
                    UploadedAt      = d.UploadedAt
                }).ToList(),
            UploadedDocumentCount = application.Documents.Count,
            CanViewDocumentFiles  = appUser.Role == "Tenant"
                || (appUser.Role == "PropertyManager"
                    && LeaseApplicationDocumentRules.ManagerCanViewDocuments(application.Status)),
            CanReUploadDocuments  = appUser.Role == "Tenant"
                && LeaseApplicationDocumentRules.TenantCanReUploadDocuments(
                    application.Status,
                    application.Documents.Select(d => (d.FileType ?? "", d.Status)))
        };

        return View("LeaseApplicationDetails", vm);
    }

    private async Task<(Document? Document, IActionResult? Error)> GetAccessibleDocumentAsync(
        int documentId, User appUser)
    {
        var document = await _db.Documents
            .Include(d => d.Application)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document?.Application == null)
            return (null, NotFound());

        if (document.Application.ParentLeaseId.HasValue)
            return (document, NotFound());

        if (appUser.Role == "Tenant")
        {
            if (document.Application.UserId != appUser.UserId)
                return (document, Forbid());
            return (document, null);
        }

        if (appUser.Role == "PropertyManager")
        {
            if (!LeaseApplicationDocumentRules.ManagerCanViewDocuments(document.Application.Status))
                return (document, Forbid());
            return (document, null);
        }

        return (document, Forbid());
    }

    // GET /LeaseApplications/ViewDocument/{id} — HTML shell so the browser tab shows the file name
    public async Task<IActionResult> ViewDocument(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var (document, error) = await GetAccessibleDocumentAsync(id, appUser);
        if (error != null) return error;
        if (document == null) return NotFound();

        if (!LeaseApplicationDocumentRules.IsActiveDocumentStatus(document.Status))
            return NotFound();

        return View("DocumentViewer", new DocumentViewerViewModel
        {
            DocumentId = document.DocumentId,
            Title      = LeaseApplicationDocumentRules.GetDocumentViewerTitle(
                document.FileName, document.FileType)
        });
    }

    // GET /LeaseApplications/ViewDocumentContent/{id} — raw PDF for iframe
    public async Task<IActionResult> ViewDocumentContent(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var (document, error) = await GetAccessibleDocumentAsync(id, appUser);
        if (error != null) return error;
        if (document == null) return NotFound();

        if (!LeaseApplicationDocumentRules.IsActiveDocumentStatus(document.Status))
            return NotFound();

        var absolutePath = _documents.ResolveAbsolutePath(document.StoragePath);
        if (absolutePath == null) return NotFound();

        Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return PhysicalFile(absolutePath, "application/pdf");
    }

    // GET /LeaseApplications/DownloadDocument/{id}
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var (document, error) = await GetAccessibleDocumentAsync(id, appUser);
        if (error != null) return error;
        if (document == null) return NotFound();

        var absolutePath = _documents.ResolveAbsolutePath(document.StoragePath);
        if (absolutePath == null) return NotFound();

        return PhysicalFile(absolutePath, "application/pdf", document.FileName);
    }

    // GET /LeaseApplications/DownloadAllDocuments/{applicationId}
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> DownloadAllDocuments(int applicationId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Documents)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();
        if (application.ParentLeaseId.HasValue) return NotFound();

        if (!LeaseApplicationDocumentRules.ManagerCanViewDocuments(application.Status))
            return Forbid();

        var files = application.Documents
            .Where(d => LeaseApplicationDocumentRules.IsActiveDocumentStatus(d.Status))
            .Select(d => new { d.FileName, d.StoragePath })
            .ToList();

        if (files.Count == 0)
        {
            TempData["Error"] = "No documents available to download.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var absolutePath = _documents.ResolveAbsolutePath(file.StoragePath);
                if (absolutePath == null) continue;

                var entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await using var fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
                await fileStream.CopyToAsync(entryStream);
            }
        }

        zipStream.Position = 0;
        var safeName = LeaseApplicationDocumentRules.SanitizeApplicantName(application.User.FullName);
        var zipFileName = $"{applicationId}_{application.UserId}_{safeName}_documents.zip";
        return File(zipStream.ToArray(), "application/zip", zipFileName);
    }

    // POST /LeaseApplications/RejectDocument
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDocument(int applicationId, string documentType, string reason)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "A rejection reason is required.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        if (!LeaseApplicationDocumentRules.IsRequiredDocumentType(documentType))
        {
            TempData["Error"] = "Invalid document type.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        var application = await _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();
        if (application.ParentLeaseId.HasValue)
        {
            TempData["Error"] = "Document review does not apply to renewal applications.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        if (application.Status != LeaseApplicationDocumentRules.ApplicationStatusScreening)
        {
            TempData["Error"] = "Documents can only be rejected while the application is under Screening.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        var docSnapshots = application.Documents
            .Select(d => (d.FileType ?? "", d.Status))
            .ToList();

        if (!LeaseApplicationDocumentRules.HasRejectableDocument(docSnapshots, documentType))
        {
            TempData["Error"] = "No active document is available to reject for this type.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        var document = application.Documents
            .Where(d => string.Equals(d.FileType, documentType, StringComparison.OrdinalIgnoreCase)
                && LeaseApplicationDocumentRules.IsActiveDocumentStatus(d.Status))
            .OrderByDescending(d => d.UploadedAt)
            .First();

        document.Status          = LeaseApplicationDocumentRules.DocumentStatusRejected;
        document.RejectionReason = reason.Trim();

        await _db.SaveChangesAsync();

        var docLabel = LeaseApplicationDocumentRules.GetDisplayName(documentType);
        var msg = $"Your {docLabel} for application #{applicationId} (Unit {application.Unit.UnitNumber}) was rejected. " +
                    $"Reason: {reason.Trim()}. Please upload a new PDF.";

        await _notifier.SendAsync(application.UserId, msg, "LeaseUpdate");

        try
        {
            await _emailService.SendDocumentsRejectedAsync(
                application.User.Email,
                application.User.FullName,
                application.Unit.UnitNumber,
                application.Unit.Property.Name,
                application.ApplicationId,
                docLabel,
                reason.Trim());
        }
        catch { /* email failure must not block the flow */ }

        TempData["Success"] = $"{docLabel} rejected. The application remains under Screening; the tenant has been notified to upload a new PDF.";
        return RedirectToAction("Details", new { id = applicationId });
    }

    // GET /LeaseApplications/ReUploadDocuments/{id}
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> ReUploadDocuments(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ApplicationId == id);

        if (application == null) return NotFound();
        if (application.UserId != appUser.UserId) return Forbid();

        var docSnapshots = application.Documents.Select(d => (d.FileType ?? "", d.Status ?? "")).ToList();
        if (!LeaseApplicationDocumentRules.TenantCanReUploadDocuments(application.Status, docSnapshots))
        {
            TempData["Error"] = "This application does not require document re-upload.";
            return RedirectToAction("Details", new { id });
        }

        return View(BuildReUploadViewModel(application));
    }

    // POST /LeaseApplications/ReUploadDocuments/{id}
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReUploadDocuments(int id, ReUploadDocumentsViewModel model)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ApplicationId == id);

        if (application == null) return NotFound();
        if (application.UserId != appUser.UserId) return Forbid();
        if (!LeaseApplicationDocumentRules.TenantCanReUploadDocuments(
                application.Status,
                application.Documents.Select(d => (d.FileType ?? "", d.Status))))
        {
            TempData["Error"] = "This application does not require document re-upload.";
            return RedirectToAction("Details", new { id = id });
        }

        var docSnapshots = application.Documents
            .Select(d => (d.FileType ?? "", d.Status ?? ""))
            .ToList();
        var rejectedTypes = LeaseApplicationDocumentRules.GetRejectedDocumentTypes(docSnapshots);

        if (rejectedTypes.Count == 0)
        {
            TempData["Error"] = "No rejected documents are available for re-upload.";
            return RedirectToAction("Details", new { id });
        }

        Document? FindRejected(string documentType) => application.Documents
            .Where(d => string.Equals(d.FileType, documentType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.Status, LeaseApplicationDocumentRules.DocumentStatusRejected,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefault();

        IFormFile? FileForType(string type)
        {
            if (LeaseApplicationDocumentRules.IsNationalIdType(type))
                return model.NationalIdFile;
            if (LeaseApplicationDocumentRules.IsSalaryIncomeType(type))
                return model.SalaryIncomeFile;
            return null;
        }

        var typesWithFiles = rejectedTypes
            .Where(t => FileForType(t) is { Length: > 0 })
            .ToList();

        if (typesWithFiles.Count == 0)
            ModelState.AddModelError(string.Empty, "Please select at least one PDF to upload.");

        foreach (var type in typesWithFiles)
        {
            var file = FileForType(type)!;
            var label = LeaseApplicationDocumentRules.GetDisplayName(type);
            var propertyName = LeaseApplicationDocumentRules.GetReUploadPropertyName(type);

            foreach (var msg in LeaseApplicationDocumentRules.ValidateSinglePdfUpload(
                         true, file.FileName, file.Length, label))
                ModelState.AddModelError(propertyName, msg);
        }

        if (!ModelState.IsValid)
            return View(BuildReUploadViewModel(application));

        foreach (var type in typesWithFiles)
        {
            var rejected = FindRejected(type);
            var file     = FileForType(type);
            if (rejected == null || file == null) continue;

            await _documents.ReplaceRejectedDocumentAsync(
                rejected, application.ApplicationId, appUser.UserId, appUser.FullName,
                file, type);
        }

        var tuples = (await _db.Documents
                .Where(d => d.ApplicationId == id)
                .Select(d => new { d.FileType, d.Status })
                .ToListAsync())
            .Where(d => d.FileType != null)
            .Select(d => (d.FileType!, d.Status ?? ""))
            .ToList();

        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        if (LeaseApplicationDocumentRules.HasAllRequiredDocuments(application.ParentLeaseId, tuples))
        {
            foreach (var mgr in managers)
                await _notifier.SendAsync(mgr.UserId,
                    $"{appUser.FullName} re-uploaded all required documents for application #{application.ApplicationId} (Unit {application.Unit.UnitNumber}). Review in Screening.",
                    "LeaseUpdate");

            TempData["Success"] = "All documents uploaded. Your application remains under Screening for manager review.";
        }
        else
        {
            foreach (var mgr in managers)
                await _notifier.SendAsync(mgr.UserId,
                    $"{appUser.FullName} re-uploaded a document for application #{application.ApplicationId} (Unit {application.Unit.UnitNumber}).",
                    "LeaseUpdate");

            TempData["Success"] = "Document uploaded. Please upload any remaining rejected documents.";
        }

        var stillRejected = LeaseApplicationDocumentRules.GetRejectedDocumentTypes(tuples);
        if (stillRejected.Count > 0)
            return RedirectToAction(nameof(ReUploadDocuments), new { id });

        return RedirectToAction("Details", new { id });
    }

    private static ReUploadDocumentsViewModel BuildReUploadViewModel(LeaseApplication application)
    {
        var rejected = application.Documents
            .Where(d => string.Equals(d.Status, LeaseApplicationDocumentRules.DocumentStatusRejected,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ReUploadDocumentsViewModel
        {
            ApplicationId               = application.ApplicationId,
            UnitNumber                  = application.Unit.UnitNumber,
            PropertyName                = application.Unit.Property.Name,
            RejectedDocumentCount       = rejected.Count,
            NeedsNationalId             = rejected.Any(d =>
                string.Equals(d.FileType, LeaseApplicationDocumentRules.NationalId,
                    StringComparison.OrdinalIgnoreCase)),
            NeedsSalaryIncome           = rejected.Any(d =>
                string.Equals(d.FileType, LeaseApplicationDocumentRules.SalaryIncome,
                    StringComparison.OrdinalIgnoreCase)),
            NationalIdRejectionReason   = rejected
                .FirstOrDefault(d => string.Equals(d.FileType, LeaseApplicationDocumentRules.NationalId,
                    StringComparison.OrdinalIgnoreCase))?.RejectionReason,
            SalaryIncomeRejectionReason = rejected
                .FirstOrDefault(d => string.Equals(d.FileType, LeaseApplicationDocumentRules.SalaryIncome,
                    StringComparison.OrdinalIgnoreCase))?.RejectionReason
        };
    }

    // ── Lease Details ─────────────────────────────────────────────────────────
    public async Task<IActionResult> LeaseDetails(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        await UpdateLeaseStatusesAsync();

        var lease = await _db.Leases
            .Include(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(l => l.Application.User)
            .Include(l => l.LeaseLogs)
                .ThenInclude(ll => ll.ChangedByUser)
            .Include(l => l.PaymentRecords)
            .Include(l => l.Termination)
            .Include(l => l.RenewLeaseApplication)
            .FirstOrDefaultAsync(l => l.LeaseId == id);

        if (lease == null) return NotFound();

        if (appUser.Role == "Tenant" && lease.Application.UserId != appUser.UserId)
            return Forbid();

        var orderedPayments = lease.PaymentRecords.OrderBy(p => p.DueDate).ToList();
        var total           = orderedPayments.Count;

        var vm = new LeaseListViewModel
        {
            LeaseId                 = lease.LeaseId,
            ApplicationId           = lease.ApplicationId,
            UnitNumber              = lease.Application.Unit.UnitNumber,
            PropertyName            = lease.Application.Unit.Property.Name,
            TenantName              = lease.Application.User.FullName,
            LeaseStartDate          = lease.LeaseStartDate,
            LeaseEndDate            = lease.LeaseEndDate,
            MonthlyRent             = lease.MonthlyRent,
            SecurityDeposit         = lease.SecurityDeposit,
            Status                  = lease.Status,
            CreatedAt               = lease.CreatedAt,
            GracePeriodDays         = lease.Application.Unit.Property.GracePeriodDays,
            LateFeePercent          = lease.Application.Unit.Property.LateFeePercent,
            TerminationId           = lease.TerminationId,
            TerminationDate         = lease.Termination?.TerminationDate,
            TerminationNotes        = lease.Termination?.Notes,
            RenewLeaseApplicationId = lease.RenewLeaseApplicationId,
            RenewApplicationStatus  = lease.RenewLeaseApplication?.Status,
            TenantEmail             = lease.Application.User.Email,
            TenantPhone             = lease.Application.User.Phone,
            Logs                    = lease.LeaseLogs
                .OrderBy(ll => ll.CreatedAt)
                .Select(ll => new LeaseLogViewModel
                {
                    Status            = ll.Status,
                    ChangedByUserName = ll.ChangedByUser.FullName,
                    Notes             = ll.Notes,
                    CreatedAt         = ll.CreatedAt
                }).ToList(),
            Payments                = orderedPayments.Select((p, i) => new PaymentSummaryViewModel
            {
                PaymentId         = p.PaymentId,
                InstallmentNum    = i + 1,
                TotalInstallments = total,
                AmountDue         = p.AmountDue,
                AmountPaid        = p.AmountPaid,
                LateFee           = p.LateFee,
                DueDate           = p.DueDate,
                PaidDate          = p.PaidDate,
                Status            = p.PaymentStatus,
                Notes             = p.Notes
            }).ToList()
        };

        return View(vm);
    }

    // ── Apply (new regular application) ──────────────────────────────────────
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Apply(int unitId)
    {
        var unit = await _db.Units
            .Include(u => u.Property)
            .FirstOrDefaultAsync(u => u.UnitId == unitId);

        if (unit == null) return NotFound();

        if (unit.AvailabilityStatus != "Available")
        {
            TempData["Error"] = "This unit is not available for leasing.";
            return RedirectToAction("UnitDetails", "Properties", new { id = unitId });
        }

        return View(new CreateLeaseApplicationViewModel
        {
            UnitId       = unit.UnitId,
            UnitNumber   = unit.UnitNumber,
            PropertyName = unit.Property.Name,
            MonthlyRent  = unit.MonthlyRent
        });
    }

    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(CreateLeaseApplicationViewModel model)
    {
        var today    = DateTime.Today;
        var minStart = today.AddDays(2);
        var maxStart = today.AddMonths(2);

        if (model.RequestedStartDate < minStart)
            ModelState.AddModelError(nameof(model.RequestedStartDate),
                $"Start date must be at least {minStart:dd MMM yyyy} (2 days from today).");

        if (model.RequestedStartDate > maxStart)
            ModelState.AddModelError(nameof(model.RequestedStartDate),
                $"Start date cannot be later than {maxStart:dd MMM yyyy} (2 months from today).");

        if (model.RequestedEndDate <= model.RequestedStartDate)
            ModelState.AddModelError(nameof(model.RequestedEndDate),
                "End date must be after the start date.");

        if (model.RequestedEndDate > model.RequestedStartDate.AddYears(1))
            ModelState.AddModelError(nameof(model.RequestedEndDate),
                "Lease period cannot exceed one year from the start date.");

        foreach (var msg in LeaseApplicationDocumentRules.ValidateRegularApplicationUploads(
                     model.NationalIdFile != null && model.NationalIdFile.Length > 0,
                     model.SalaryIncomeFile != null && model.SalaryIncomeFile.Length > 0,
                     model.NationalIdFile?.FileName,
                     model.NationalIdFile?.Length ?? 0,
                     model.SalaryIncomeFile?.FileName,
                     model.SalaryIncomeFile?.Length ?? 0))
        {
            ModelState.AddModelError(string.Empty, msg);
        }

        if (!ModelState.IsValid)
        {
            await ReloadApplyUnitSummaryAsync(model);
            return View(model);
        }

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var existing = await _db.LeaseApplications.AnyAsync(a =>
            a.UnitId == model.UnitId &&
            a.UserId == appUser.UserId &&
            (a.Status == "Pending" || a.Status == "Screening" || a.Status == "Approved"
             || a.Status == LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired));

        if (existing)
        {
            TempData["Error"] = "You already have an active application for this unit.";
            return RedirectToAction("Index");
        }

        var application = new LeaseApplication
        {
            UserId             = appUser.UserId,
            UnitId             = model.UnitId,
            RequestedStartDate = model.RequestedStartDate,
            RequestedEndDate   = model.RequestedEndDate,
            Notes              = model.Notes,
            Status             = "Pending",
            ParentLeaseId      = null,
            CreatedAt          = DateTime.Now
        };

        _db.LeaseApplications.Add(application);
        await _db.SaveChangesAsync();

        await _documents.SaveRegularApplicationDocumentsAsync(
            application.ApplicationId,
            appUser.UserId,
            appUser.FullName,
            model.NationalIdFile!,
            model.SalaryIncomeFile!);

        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Pending",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });
        await _db.SaveChangesAsync();

        await _notifier.SendAsync(appUser.UserId,
            "Your lease application has been submitted and is under review.", "LeaseUpdate");

        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"New lease application from {appUser.FullName} for unit {model.UnitNumber}.",
                "LeaseUpdate");

        // Send confirmation email to tenant
        try { await _emailService.SendApplicationSubmittedAsync(
            appUser.Email, appUser.FullName, model.UnitNumber,
            model.PropertyName ?? "", application.ApplicationId); }
        catch { /* email failure should not block the flow */ }

        TempData["Success"] = "Application submitted successfully. Status: Pending.";
        return RedirectToAction("Index");
    }

    // ── Apply Renew (renewal application from an active lease) ───────────────
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> ApplyRenew(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "Active")
        {
            TempData["Error"] = "You can only apply for renewal on an active lease.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }
        if (lease.RenewLeaseApplicationId != null)
        {
            TempData["Error"] = "A renewal application already exists for this lease.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }
        if (lease.TerminationId != null)
        {
            TempData["Error"] = "Cannot apply for renewal when a termination is scheduled.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }

        var remaining = (lease.LeaseEndDate - DateTime.Today).Days;
        if (remaining > 183 || remaining < 2)
        {
            TempData["Error"] = "Renewal is only available when 2 days to 6 months remain on your lease.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }

        var renewStart = lease.LeaseEndDate.AddDays(1);

        return View(new ApplyRenewViewModel
        {
            LeaseId            = lease.LeaseId,
            UnitNumber         = lease.Application.Unit.UnitNumber,
            PropertyName       = lease.Application.Unit.Property.Name,
            MonthlyRent        = lease.Application.Unit.MonthlyRent,
            RequestedStartDate = renewStart,
            RequestedEndDate   = renewStart.AddMonths(6)
        });
    }

    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyRenew(ApplyRenewViewModel model)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        if (model.RequestedEndDate <= model.RequestedStartDate)
            ModelState.AddModelError(nameof(model.RequestedEndDate),
                "End date must be after the start date.");

        if (model.RequestedEndDate > model.RequestedStartDate.AddYears(1))
            ModelState.AddModelError(nameof(model.RequestedEndDate),
                "Lease period cannot exceed one year from the start date.");

        if (!ModelState.IsValid) return View(model);

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit)
            .FirstOrDefaultAsync(l => l.LeaseId == model.LeaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "Active" || lease.RenewLeaseApplicationId != null || lease.TerminationId != null)
        {
            TempData["Error"] = "Cannot create renewal application at this time.";
            return RedirectToAction("LeaseDetails", new { id = model.LeaseId });
        }

        var application = new LeaseApplication
        {
            UserId             = appUser.UserId,
            UnitId             = lease.Application.UnitId,
            RequestedStartDate = model.RequestedStartDate,
            RequestedEndDate   = model.RequestedEndDate,
            Notes              = model.Notes,
            Status             = "Pending",
            ParentLeaseId      = lease.LeaseId,
            CreatedAt          = DateTime.Now
        };

        _db.LeaseApplications.Add(application);
        await _db.SaveChangesAsync();

        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Pending",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });

        lease.RenewLeaseApplicationId = application.ApplicationId;
        await _db.SaveChangesAsync();

        await _notifier.SendAsync(appUser.UserId,
            $"Your renewal application for unit {lease.Application.Unit.UnitNumber} has been submitted.",
            "LeaseUpdate");

        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"Renewal application submitted by {appUser.FullName} for unit {lease.Application.Unit.UnitNumber}.",
                "LeaseUpdate");

        // Email confirmation to tenant
        try
        {
            await _emailService.SendRenewalSubmittedAsync(
                toEmail:     appUser.Email,
                toName:      appUser.FullName,
                unitNumber:  lease.Application.Unit.UnitNumber,
                propertyName: lease.Application.Unit.Property?.Name ?? "",
                newEndDate:  model.RequestedEndDate,
                applicationId: application.ApplicationId);
        }
        catch { /* email failure must not block the flow */ }

        TempData["Success"] = "Renewal application submitted successfully. Status: Pending.";
        return RedirectToAction("LeaseDetails", new { id = model.LeaseId });
    }

    // ── Cancel Application (tenant) ───────────────────────────────────────────
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelApplication(int applicationId, string returnTo = "Index")
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();
        if (application.UserId != appUser.UserId) return Forbid();

        if (application.Status == "Rejected" || application.Status == "Canceled")
        {
            TempData["Error"] = "This application cannot be canceled.";
            return returnTo == "Details"
                ? RedirectToAction("Details", new { id = applicationId })
                : RedirectToAction("Index", new { tab = "applications" });
        }

        // If application was Approved, also terminate the PendingPayment lease
        if (application.Status == "Approved")
        {
            var pendingLease = await _db.Leases
                .FirstOrDefaultAsync(l => l.ApplicationId == applicationId && l.Status == "PendingPayment");

            if (pendingLease != null)
            {
                pendingLease.Status = "Terminated";
                _db.LeaseLogs.Add(new LeaseLog
                {
                    LeaseId         = pendingLease.LeaseId,
                    Status          = "Terminated",
                    ChangedByUserId = appUser.UserId,
                    Notes           = "Lease terminated — application canceled by tenant before payment.",
                    CreatedAt       = DateTime.Now
                });
            }
        }

        // If this was a renewal application, clear the parent lease's RenewLeaseApplicationId
        if (application.ParentLeaseId.HasValue)
        {
            var parentLease = await _db.Leases
                .FirstOrDefaultAsync(l => l.LeaseId == application.ParentLeaseId &&
                                          l.RenewLeaseApplicationId == application.ApplicationId);
            if (parentLease != null)
                parentLease.RenewLeaseApplicationId = null;
        }

        application.Status = "Canceled";

        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Canceled",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Application canceled successfully.";
        return returnTo == "Details"
            ? RedirectToAction("Details", new { id = applicationId })
            : RedirectToAction("Index", new { tab = "applications" });
    }

    // ── Terminate Lease Now (Approved lease → Terminated immediately) ─────────
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateNow(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();

        if (lease.Status != "Approved")
        {
            TempData["Error"] = "Only approved (not-yet-started) leases can be immediately terminated.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }

        lease.Status = "Terminated";
        if (lease.Application.Unit != null)
            lease.Application.Unit.AvailabilityStatus = "Available";

        _db.LeaseLogs.Add(new LeaseLog
        {
            LeaseId         = lease.LeaseId,
            Status          = "Terminated",
            ChangedByUserId = appUser.UserId,
            Notes           = "Lease terminated by tenant before start date.",
            CreatedAt       = DateTime.Now
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Lease has been terminated.";
        return RedirectToAction("LeaseDetails", new { id = leaseId });
    }

    // ── Terminate Active Lease (schedule termination date) ────────────────────
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> TerminateLease(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .Include(l => l.Termination)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();

        if (lease.Status != "Active")
        {
            TempData["Error"] = "Only active leases can schedule a termination.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }

        var vm = new TerminateLeaseViewModel
        {
            LeaseId        = lease.LeaseId,
            UnitNumber     = lease.Application.Unit.UnitNumber,
            PropertyName   = lease.Application.Unit.Property.Name,
            LeaseEndDate   = lease.LeaseEndDate,
            TerminationId  = lease.TerminationId,
            TerminationDate = lease.Termination?.TerminationDate ?? DateTime.Today.AddDays(2),
            Notes          = lease.Termination?.Notes
        };

        return View(vm);
    }

    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateLease(TerminateLeaseViewModel model)
    {
        var minDate = DateTime.Today.AddDays(2);

        if (model.TerminationDate < minDate)
            ModelState.AddModelError(nameof(model.TerminationDate),
                $"Termination date must be at least {minDate:dd MMM yyyy} (2 days from today).");

        if (model.TerminationDate > model.LeaseEndDate)
            ModelState.AddModelError(nameof(model.TerminationDate),
                $"Termination date cannot be after the lease end date ({model.LeaseEndDate:dd MMM yyyy}).");

        if (!ModelState.IsValid) return View(model);

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application)
            .Include(l => l.Termination)
            .FirstOrDefaultAsync(l => l.LeaseId == model.LeaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "Active")
        {
            TempData["Error"] = "Only active leases can schedule a termination.";
            return RedirectToAction("LeaseDetails", new { id = model.LeaseId });
        }

        if (lease.TerminationId.HasValue && lease.Termination != null)
        {
            // Edit existing termination
            lease.Termination.TerminationDate = model.TerminationDate;
            lease.Termination.Notes           = model.Notes;
            TempData["Success"] = "Termination schedule updated.";
        }
        else
        {
            // Create new termination
            var termination = new Termination
            {
                TerminationDate = model.TerminationDate,
                Notes           = model.Notes,
                CreatedAt       = DateTime.Now
            };
            _db.Terminations.Add(termination);
            await _db.SaveChangesAsync();

            lease.TerminationId = termination.TerminationId;
            TempData["Success"] = $"Termination scheduled for {model.TerminationDate:dd MMM yyyy}.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction("LeaseDetails", new { id = model.LeaseId });
    }

    // ── Cancel Scheduled Termination ──────────────────────────────────────────
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTermination(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application)
            .Include(l => l.Termination)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();

        if (lease.TerminationId.HasValue && lease.Termination != null)
        {
            var termination = lease.Termination;
            lease.TerminationId = null;
            await _db.SaveChangesAsync();

            _db.Terminations.Remove(termination);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Termination canceled. Your lease will continue until its original end date.";
        }
        else
        {
            TempData["Error"] = "No scheduled termination found.";
        }

        return RedirectToAction("LeaseDetails", new { id = leaseId });
    }

    // ── Start Screening ───────────────────────────────────────────────────────
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartScreening(int applicationId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();

        if (application.Status != "Pending")
        {
            TempData["Error"] = "Only Pending applications can be moved to Screening.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        if (!LeaseApplicationDocumentRules.IsRenewalApplication(application.ParentLeaseId))
        {
            var presentDocs = await _db.Documents
                .Where(d => d.ApplicationId == applicationId && d.FileType != null)
                .Select(d => new { d.FileType, d.Status })
                .ToListAsync();

            var presentTuples = presentDocs.Select(d => (d.FileType!, d.Status)).ToList();

            if (!LeaseApplicationDocumentRules.HasAllRequiredDocuments(application.ParentLeaseId, presentTuples))
            {
                TempData["Error"] =
                    "Cannot start screening: the tenant has not uploaded all required documents (National ID and salary / income proof).";
                return RedirectToAction("Details", new { id = applicationId });
            }
        }

        application.Status = "Screening";

        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Screening",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });

        await _db.SaveChangesAsync();

        await _notifier.SendAsync(application.UserId,
            $"Your application for unit {application.Unit.UnitNumber} is now under screening.",
            "LeaseUpdate");

        // Send screening email to tenant
        try { await _emailService.SendApplicationScreeningAsync(
            application.User.Email, application.User.FullName,
            application.Unit.UnitNumber, application.Unit.Property?.Name ?? "",
            application.ApplicationId); }
        catch { /* email failure should not block the flow */ }

        TempData["Success"] = "Application moved to Screening.";
        return RedirectToAction("Details", new { id = applicationId });
    }

    // ── Approve ───────────────────────────────────────────────────────────────
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int applicationId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();

        if (application.Status != "Screening")
        {
            TempData["Error"] = "Only applications in Screening status can be Approved.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        application.Status = "Approved";

        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Approved",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });

        var startDate = application.RequestedStartDate ?? DateTime.Now;
        var endDate   = application.RequestedEndDate   ?? DateTime.Now.AddYears(1);
        var rent      = application.Unit.MonthlyRent ?? 0;

        var lease = new Lease
        {
            ApplicationId   = application.ApplicationId,
            LeaseStartDate  = startDate,
            LeaseEndDate    = endDate,
            MonthlyRent     = rent,
            SecurityDeposit = rent * 2,
            Status          = "PendingPayment",
            CreatedAt       = DateTime.Now
        };
        _db.Leases.Add(lease);
        await _db.SaveChangesAsync();

        if (!application.ParentLeaseId.HasValue && application.Documents.Count > 0)
        {
            var archived = _documents.ArchiveApprovedApplicationDocuments(
                application.UserId,
                application.User.FullName,
                application.Documents);
            if (archived == 0)
                TempData["Warning"] = "Application approved, but document files could not be archived.";
        }

        _db.LeaseLogs.Add(new LeaseLog
        {
            LeaseId         = lease.LeaseId,
            Status          = "PendingPayment",
            ChangedByUserId = appUser.UserId,
            Notes           = "Lease created upon approval. Awaiting tenant payment.",
            CreatedAt       = DateTime.Now
        });

        var conflicting = await _db.LeaseApplications
            .Include(a => a.User)
            .Where(a =>
                a.UnitId         == application.UnitId &&
                a.ApplicationId  != application.ApplicationId &&
                (a.Status == "Pending" || a.Status == "Screening") &&
                a.RequestedStartDate < application.RequestedEndDate &&
                a.RequestedEndDate   > application.RequestedStartDate)
            .ToListAsync();

        foreach (var conflict in conflicting)
        {
            conflict.Status = "Rejected";
            _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
            {
                ApplicationId   = conflict.ApplicationId,
                Status          = "Rejected",
                ChangedByUserId = appUser.UserId,
                CreatedAt       = DateTime.Now
            });
            await _notifier.SendAsync(conflict.UserId,
                $"Your application for unit {application.Unit.UnitNumber} was automatically rejected " +
                "because another application was approved for the overlapping period.",
                "LeaseUpdate");
        }

        await _db.SaveChangesAsync();

        await _notifier.SendAsync(application.UserId,
            $"Congratulations! Your lease application for unit {application.Unit.UnitNumber} has been approved. " +
            "Please complete your payment to activate your lease.",
            "PaymentReminder");

        // Send approval email to tenant
        try { await _emailService.SendApplicationApprovedAsync(
            application.User.Email, application.User.FullName,
            application.Unit.UnitNumber, application.Unit.Property?.Name ?? "",
            application.ApplicationId); }
        catch { /* email failure should not block the flow */ }

        // Pre-tenancy maintenance cancellation happens at payment confirmation, not here.
        // (Tenant must pay first to confirm renewal before we cancel the maintenance.)

        TempData["Success"] =
            $"Application approved. Lease created. {conflicting.Count} conflicting application(s) auto-rejected.";
        return RedirectToAction("Details", new { id = applicationId });
    }

    // ── Reject ────────────────────────────────────────────────────────────────
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int applicationId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var application = await _db.LeaseApplications
            .Include(a => a.Unit)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();

        if (application.Status != "Screening")
        {
            TempData["Error"] = "Only applications in Screening status can be Rejected.";
            return RedirectToAction("Details", new { id = applicationId });
        }

        application.Status = "Rejected";

        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Rejected",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });

        await _db.SaveChangesAsync();

        await _notifier.SendAsync(application.UserId,
            $"Your lease application for unit {application.Unit.UnitNumber} has been rejected.",
            "LeaseUpdate");

        // Send rejection email to tenant
        try { await _emailService.SendApplicationRejectedAsync(
            application.User.Email, application.User.FullName,
            application.Unit.UnitNumber, application.Unit.Property?.Name ?? "",
            application.ApplicationId); }
        catch { /* email failure should not block the flow */ }

        TempData["Success"] = "Application rejected.";
        return RedirectToAction("Details", new { id = applicationId });
    }

    // ── Cancel Lease with Refund (Active or Approved-paid) ───────────────────

    // GET: calculate refund preview and return JSON for modal
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> CancelLeasePreview(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .Include(l => l.PaymentRecords)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "Active" && lease.Status != "Approved")
            return Json(new { error = "This lease cannot be cancelled." });

        var preview = ComputeRefundPreview(lease);
        return Json(preview);
    }

    // POST: execute cancellation
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelLease(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .Include(l => l.Application.User)
            .Include(l => l.PaymentRecords)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();

        if (lease.Status != "Active" && lease.Status != "Approved")
        {
            TempData["Error"] = "This lease cannot be cancelled.";
            return RedirectToAction("LeaseDetails", new { id = leaseId });
        }

        var preview = ComputeRefundPreview(lease);
        var now     = DateTime.Now;

        // 1. Cancel all Upcoming payments
        foreach (var p in lease.PaymentRecords.Where(p => p.PaymentStatus == "Upcoming"))
            p.PaymentStatus = "Cancelled";

        // 2. Create refund record
        _db.LeaseRefunds.Add(new LeaseRefund
        {
            LeaseId         = lease.LeaseId,
            MonthsConsumed  = preview.MonthsConsumed,
            MonthsRefunded  = preview.MonthsRefunded,
            TotalPaid       = preview.TotalPaid,
            OverdueDeducted = 0,
            RefundAmount    = preview.RefundAmount,
            CancelledAt     = now,
            Notes           = preview.BeforeStart
                ? "Lease cancelled before start date — full refund issued."
                : $"Lease cancelled early by tenant. Months consumed: {preview.MonthsConsumed}, months refunded: {preview.MonthsRefunded}."
        });

        // 3. Terminate lease + free unit
        lease.Status = "Terminated";
        if (lease.Application.Unit != null)
            lease.Application.Unit.AvailabilityStatus = "Available";

        _db.LeaseLogs.Add(new LeaseLog
        {
            LeaseId         = lease.LeaseId,
            Status          = "Terminated",
            ChangedByUserId = appUser.UserId,
            Notes           = preview.RefundAmount > 0
                ? $"Lease cancelled by tenant. Refund BD {preview.RefundAmount:N2} to be returned within 5–7 business days."
                : "Lease cancelled by tenant. No refund due.",
            CreatedAt       = now
        });

        // 4. Cancel any pre-tenancy maintenance
        await CancelPreTenancyMaintenanceAsync(lease.Application.UnitId, "Lease cancelled by tenant", appUser.UserId);

        await _db.SaveChangesAsync();

        string unitNum = lease.Application.Unit?.UnitNumber ?? $"Lease #{lease.LeaseId}";

        // 5. Notify tenant
        await _notifier.SendAsync(appUser.UserId,
            preview.RefundAmount > 0
                ? $"Your lease for unit {unitNum} has been cancelled. BD {preview.RefundAmount:N2} will be returned to your bank card within 5–7 business days."
                : $"Your lease for unit {unitNum} has been cancelled. No refund is due.",
            "LeaseUpdate");

        // 6. Notify managers
        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"Tenant {appUser.FullName} cancelled lease #{lease.LeaseId} for unit {unitNum}." +
                (preview.RefundAmount > 0 ? $" Refund due: BD {preview.RefundAmount:N2}." : " No refund due."),
                "LeaseUpdate");

        // 7. Email tenant
        try
        {
            await _emailService.SendLeaseCancelledAsync(
                toEmail:        appUser.Email,
                toName:         appUser.FullName,
                unitNumber:     lease.Application.Unit?.UnitNumber ?? unitNum,
                propertyName:   lease.Application.Unit?.Property?.Name ?? "",
                leaseId:        lease.LeaseId,
                refundAmount:   preview.RefundAmount,
                monthsRefunded: preview.MonthsRefunded);
        }
        catch { /* email failure must not block the flow */ }

        TempData["Success"] = preview.RefundAmount > 0
            ? $"Lease cancelled. <strong>BD {preview.RefundAmount:N2}</strong> will be refunded to your bank card within 5–7 business days."
            : "Lease cancelled. No refund is due for this lease.";

        return RedirectToAction("LeaseDetails", new { id = leaseId });
    }

    // Helper: compute refund breakdown (no overdue deduction — not applicable)
    private static RefundPreview ComputeRefundPreview(Lease lease)
    {
        var today      = DateTime.Today;
        bool beforeStart = today < lease.LeaseStartDate.Date;

        // Total actually paid (Paid records only)
        decimal totalPaid = lease.PaymentRecords
            .Where(p => p.PaymentStatus == "Paid")
            .Sum(p => p.AmountPaid ?? p.AmountDue);

        // If lease hasn't started yet → full refund, no months consumed
        if (beforeStart)
        {
            int refundedMonths = lease.MonthlyRent > 0
                ? (int)Math.Round(totalPaid / lease.MonthlyRent, MidpointRounding.AwayFromZero)
                : 0;
            return new RefundPreview
            {
                MonthsConsumed = 0,
                MonthsRefunded = refundedMonths,
                TotalPaid      = totalPaid,
                RefundAmount   = totalPaid,
                MonthlyRent    = lease.MonthlyRent,
                BeforeStart    = true
            };
        }

        // Months consumed = full months from lease start up to and including current month
        var startMonth = new DateTime(lease.LeaseStartDate.Year, lease.LeaseStartDate.Month, 1);
        var thisMonth  = new DateTime(today.Year, today.Month, 1);
        int monthsConsumed = ((thisMonth.Year - startMonth.Year) * 12)
                           + (thisMonth.Month - startMonth.Month) + 1;
        monthsConsumed = Math.Max(1, monthsConsumed);

        // Cost of consumed months (capped at what was paid — avoids negative refund)
        decimal consumedCost  = Math.Min(monthsConsumed * lease.MonthlyRent, totalPaid);
        decimal refundAmount  = Math.Max(0, totalPaid - consumedCost);

        int monthsRefunded = lease.MonthlyRent > 0
            ? (int)Math.Floor(refundAmount / lease.MonthlyRent)
            : 0;

        return new RefundPreview
        {
            MonthsConsumed = monthsConsumed,
            MonthsRefunded = monthsRefunded,
            TotalPaid      = totalPaid,
            RefundAmount   = refundAmount,
            MonthlyRent    = lease.MonthlyRent,
            BeforeStart    = false
        };
    }

    // ── Refund Report (manager only) ──────────────────────────────────────────
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> RefundReport()
    {
        var refunds = await _db.LeaseRefunds
            .Include(r => r.Lease)
                .ThenInclude(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(r => r.Lease.Application.User)
            .OrderByDescending(r => r.CancelledAt)
            .ToListAsync();

        return View(refunds);
    }

    // ── Legacy UpdateStatus shim ──────────────────────────────────────────────
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int applicationId, string newStatus)
    {
        return newStatus switch
        {
            "Screening" => await StartScreening(applicationId),
            "Approved"  => await Approve(applicationId),
            "Rejected"  => await Reject(applicationId),
            _           => BadRequest()
        };
    }
}
