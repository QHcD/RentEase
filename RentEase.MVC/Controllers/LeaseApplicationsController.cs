using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.MVC.Services;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

[Authorize]
public class LeaseApplicationsController : Controller
{
    private readonly PropertyLeasingDbContext _db;
    private readonly UserManager<AppUser>     _userManager;
    private readonly NotificationService      _notifier;
    private readonly EmailService             _emailService;

    public LeaseApplicationsController(
        PropertyLeasingDbContext db,
        UserManager<AppUser>     userManager,
        NotificationService      notifier,
        EmailService             emailService)
    {
        _db           = db;
        _userManager  = userManager;
        _notifier     = notifier;
        _emailService = emailService;
    }

    private async Task<User?> GetAppUserAsync()
    {
        var identity = await _userManager.GetUserAsync(User);
        if (identity == null) return null;

        var appUser = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identity.Id)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);

        if (appUser != null && appUser.IdentityUserId != identity.Id)
        {
            appUser.IdentityUserId = identity.Id;
            await _db.SaveChangesAsync();
        }

        return appUser;
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

        // ── Load applications ─────────────────────────────────────────────
        var appQuery = _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .AsQueryable();

        if (!isManager)
            appQuery = appQuery.Where(a => a.UserId == appUser.UserId);

        var allApps = await appQuery.OrderByDescending(a => a.CreatedAt).ToListAsync();

        var appCounts = new Dictionary<string, int>
        {
            ["All"]       = allApps.Count,
            ["Pending"]   = allApps.Count(a => a.Status == "Pending"),
            ["Screening"] = allApps.Count(a => a.Status == "Screening"),
            ["Approved"]  = allApps.Count(a => a.Status == "Approved"),
            ["Rejected"]  = allApps.Count(a => a.Status == "Rejected"),
            ["Canceled"]  = allApps.Count(a => a.Status == "Canceled")
        };

        var filteredApps = appStatus == "All"
            ? allApps
            : allApps.Where(a => a.Status == appStatus).ToList();

        var appListVms = filteredApps.Select(a => new LeaseApplicationListViewModel
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
        }).ToList();

        var appGroups = new List<UnitApplicationGroupViewModel>();
        if (isManager)
        {
            appGroups = filteredApps
                .GroupBy(a => a.UnitId)
                .Select(g => new UnitApplicationGroupViewModel
                {
                    UnitId             = g.Key,
                    UnitNumber         = g.First().Unit.UnitNumber,
                    PropertyName       = g.First().Unit.Property.Name,
                    AvailabilityStatus = g.First().Unit.AvailabilityStatus,
                    ApplicationCount   = g.Count(),
                    Applications       = g.Select(a => new LeaseApplicationListViewModel
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
                    }).ToList()
                }).ToList();
        }

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
            IsManager         = isManager,
            ActiveTab         = tab,
            AppStatusFilter   = appStatus,
            AppCounts         = appCounts,
            Applications      = appListVms,
            ApplicationGroups = appGroups,
            LeaseStatusFilter = leaseStatus,
            LeaseCounts       = leaseCounts,
            Leases            = leaseVms
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
            .Include(a => a.ApplicationLogs).ThenInclude(l => l.ChangedByUser)
            .FirstOrDefaultAsync(a => a.ApplicationId == id);

        if (application == null) return NotFound();

        if (appUser.Role == "Tenant" && application.UserId != appUser.UserId)
            return Forbid();

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
                }).ToList()
        };

        return View("LeaseApplicationDetails", vm);
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

        if (!ModelState.IsValid) return View(model);

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var existing = await _db.LeaseApplications.AnyAsync(a =>
            a.UnitId == model.UnitId &&
            a.UserId == appUser.UserId &&
            (a.Status == "Pending" || a.Status == "Screening" || a.Status == "Approved"));

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
            .Include(a => a.Unit)
            .Include(a => a.User)
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
