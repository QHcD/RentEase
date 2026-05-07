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

    public LeaseApplicationsController(
        PropertyLeasingDbContext db,
        UserManager<AppUser> userManager,
        NotificationService notifier)
    {
        _db          = db;
        _userManager = userManager;
        _notifier    = notifier;
    }

    private async Task<User?> GetAppUserAsync()
    {
        var identity = await _userManager.GetUserAsync(User);
        if (identity == null) return null;

        // Primary lookup by IdentityUserId; fall back to email for rows that were
        // seeded via SQL scripts and may have a null IdentityUserId.
        var appUser = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identity.Id)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);

        if (appUser != null && appUser.IdentityUserId != identity.Id)
        {
            appUser.IdentityUserId = identity.Id;
            await _db.SaveChangesAsync();
        }

        return appUser;
    }

    // ── Unified Index: Applications & Leases ─────────────────────────────────
    // GET /LeaseApplications?tab=applications&appStatus=Pending&leaseStatus=All
    public async Task<IActionResult> Index(
        string tab          = "applications",
        string appStatus    = "All",
        string leaseStatus  = "All")
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        bool isManager = appUser.Role == "PropertyManager";

        // ── Load applications ─────────────────────────────────────────────
        var appQuery = _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .AsQueryable();

        if (!isManager)
            appQuery = appQuery.Where(a => a.UserId == appUser.UserId);

        var allApps = await appQuery.OrderByDescending(a => a.CreatedAt).ToListAsync();

        // Status counts for badge labels (full unfiltered set)
        var appCounts = new Dictionary<string, int>
        {
            ["All"]       = allApps.Count,
            ["Pending"]   = allApps.Count(a => a.Status == "Pending"),
            ["Screening"] = allApps.Count(a => a.Status == "Screening"),
            ["Approved"]  = allApps.Count(a => a.Status == "Approved"),
            ["Rejected"]  = allApps.Count(a => a.Status == "Rejected")
        };

        // Apply status filter
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
            CreatedAt          = a.CreatedAt
        }).ToList();

        // Manager: also build grouped-by-unit view
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
                        CreatedAt          = a.CreatedAt
                    }).ToList()
                })
                .ToList();
        }

        // ── Load leases ───────────────────────────────────────────────────
        var leaseQuery = _db.Leases
            .Include(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(l => l.Application.User)
            .Include(l => l.LeaseLogs)
                .ThenInclude(ll => ll.ChangedByUser)
            .AsQueryable();

        if (!isManager)
            leaseQuery = leaseQuery.Where(l => l.Application.UserId == appUser.UserId);

        var allLeases = await leaseQuery.OrderByDescending(l => l.CreatedAt).ToListAsync();

        var leaseCounts = new Dictionary<string, int>
        {
            ["All"]        = allLeases.Count,
            ["Active"]     = allLeases.Count(l => l.Status == "Active"),
            ["Expired"]    = allLeases.Count(l => l.Status == "Expired"),
            ["Terminated"] = allLeases.Count(l => l.Status == "Terminated"),
            ["Renewed"]    = allLeases.Count(l => l.Status == "Renewed")
        };

        var filteredLeases = leaseStatus == "All"
            ? allLeases
            : allLeases.Where(l => l.Status == leaseStatus).ToList();

        var leaseVms = filteredLeases.Select(l => new LeaseListViewModel
        {
            LeaseId         = l.LeaseId,
            ApplicationId   = l.ApplicationId,
            UnitNumber      = l.Application.Unit.UnitNumber,
            PropertyName    = l.Application.Unit.Property.Name,
            TenantName      = l.Application.User.FullName,
            LeaseStartDate  = l.LeaseStartDate,
            LeaseEndDate    = l.LeaseEndDate,
            MonthlyRent     = l.MonthlyRent,
            SecurityDeposit = l.SecurityDeposit,
            Status          = l.Status,
            CreatedAt       = l.CreatedAt,
            Logs            = l.LeaseLogs
                .OrderBy(ll => ll.CreatedAt)
                .Select(ll => new LeaseLogViewModel
                {
                    Status            = ll.Status,
                    ChangedByUserName = ll.ChangedByUser.FullName,
                    Notes             = ll.Notes,
                    CreatedAt         = ll.CreatedAt
                }).ToList()
        }).ToList();

        // ── Assemble unified view model ───────────────────────────────────
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

    // ── Details ──────────────────────────────────────────────────────────────
    // GET /LeaseApplications/Details/{id}
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

        // Tenants can only see their own applications
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
            Logs               = application.ApplicationLogs
                .OrderBy(l => l.CreatedAt)
                .Select(l => new LeaseApplicationLogViewModel
                {
                    Status            = l.Status,
                    ChangedByUserName = l.ChangedByUser.FullName,
                    CreatedAt         = l.CreatedAt
                })
                .ToList()
        };

        return View("LeaseApplicationDetails", vm);
    }

    // ── Lease Details ─────────────────────────────────────────────────────────
    // GET /LeaseApplications/LeaseDetails/{id}
    public async Task<IActionResult> LeaseDetails(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(l => l.Application.User)
            .Include(l => l.LeaseLogs)
                .ThenInclude(ll => ll.ChangedByUser)
            .Include(l => l.PaymentRecords)
            .FirstOrDefaultAsync(l => l.LeaseId == id);

        if (lease == null) return NotFound();

        if (appUser.Role == "Tenant" && lease.Application.UserId != appUser.UserId)
            return Forbid();

        var vm = new LeaseListViewModel
        {
            LeaseId         = lease.LeaseId,
            ApplicationId   = lease.ApplicationId,
            UnitNumber      = lease.Application.Unit.UnitNumber,
            PropertyName    = lease.Application.Unit.Property.Name,
            TenantName      = lease.Application.User.FullName,
            LeaseStartDate  = lease.LeaseStartDate,
            LeaseEndDate    = lease.LeaseEndDate,
            MonthlyRent     = lease.MonthlyRent,
            SecurityDeposit = lease.SecurityDeposit,
            Status          = lease.Status,
            CreatedAt       = lease.CreatedAt,
            Logs            = lease.LeaseLogs
                .OrderBy(ll => ll.CreatedAt)
                .Select(ll => new LeaseLogViewModel
                {
                    Status            = ll.Status,
                    ChangedByUserName = ll.ChangedByUser.FullName,
                    Notes             = ll.Notes,
                    CreatedAt         = ll.CreatedAt
                }).ToList()
        };

        return View(vm);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────
    // GET /LeaseApplications/Apply/{unitId}
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

    // POST /LeaseApplications/Apply
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

        // No duplicate active application for the same unit by the same tenant
        var existing = await _db.LeaseApplications.AnyAsync(a =>
            a.UnitId  == model.UnitId &&
            a.UserId  == appUser.UserId &&
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
            CreatedAt          = DateTime.Now
        };

        _db.LeaseApplications.Add(application);
        await _db.SaveChangesAsync();

        // Log initial Pending status
        _db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = application.ApplicationId,
            Status          = "Pending",
            ChangedByUserId = appUser.UserId,
            CreatedAt       = DateTime.Now
        });
        await _db.SaveChangesAsync();

        await _notifier.SendAsync(appUser.UserId,
            "Your lease application has been submitted and is under review.",
            "LeaseUpdate");

        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"New lease application from {appUser.FullName} for unit {model.UnitNumber}.",
                "LeaseUpdate");

        TempData["Success"] = "Application submitted successfully. Status: Pending.";
        return RedirectToAction("Index");
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

        // Create lease and mark unit as Occupied
        var lease = new Lease
        {
            ApplicationId   = application.ApplicationId,
            LeaseStartDate  = application.RequestedStartDate ?? DateTime.Now,
            LeaseEndDate    = application.RequestedEndDate   ?? DateTime.Now.AddYears(1),
            MonthlyRent     = application.Unit.MonthlyRent ?? 0,
            SecurityDeposit = (application.Unit.MonthlyRent ?? 0) * 2,
            Status          = "Active",
            CreatedAt       = DateTime.Now
        };
        _db.Leases.Add(lease);
        application.Unit.AvailabilityStatus = "Occupied";

        // Save here so the DB assigns lease.LeaseId before child records reference it.
        await _db.SaveChangesAsync();

        // First payment record — lease.LeaseId is now the real identity value.
        _db.PaymentRecords.Add(new PaymentRecord
        {
            LeaseId       = lease.LeaseId,
            AmountDue     = lease.MonthlyRent,
            DueDate       = lease.LeaseStartDate,
            PaymentStatus = "Pending"
        });

        // Log the lease creation.
        _db.LeaseLogs.Add(new LeaseLog
        {
            LeaseId         = lease.LeaseId,
            Status          = "Active",
            ChangedByUserId = appUser.UserId,
            Notes           = "Lease created upon application approval.",
            CreatedAt       = DateTime.Now
        });

        // Auto-reject conflicting applications for the same unit
        var conflicting = await _db.LeaseApplications
            .Include(a => a.User)
            .Where(a =>
                a.UnitId          == application.UnitId &&
                a.ApplicationId   != application.ApplicationId &&
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
            $"Congratulations! Your lease application for unit {application.Unit.UnitNumber} has been approved.",
            "LeaseUpdate");

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
