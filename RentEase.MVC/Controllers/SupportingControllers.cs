using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.MVC.Services;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

// ── Notifications ─────────────────────────────────────
[Authorize]
public class NotificationsController : Controller
{
    private readonly PropertyLeasingDbContext _db;
    private readonly UserManager<AppUser>     _userManager;

    public NotificationsController(PropertyLeasingDbContext db, UserManager<AppUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
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

    // GET /Notifications
    public async Task<IActionResult> Index()
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var notifications = await _db.Notifications
            .Where(n => n.UserId == appUser.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationViewModel
            {
                NotificationId   = n.NotificationId,
                Message          = n.Message,
                NotificationType = n.NotificationType,
                Status           = n.Status,
                CreatedAt        = n.CreatedAt
            })
            .ToListAsync();

        return View(notifications);
    }

    // POST /Notifications/MarkRead/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var appUser = await GetAppUserAsync();
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == appUser!.UserId);

        if (notification != null)
        {
            notification.Status = "Read";
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index");
    }

    // POST /Notifications/MarkAllRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var unread = await _db.Notifications
            .Where(n => n.UserId == appUser.UserId && n.Status == "Unread")
            .ToListAsync();

        unread.ForEach(n => n.Status = "Read");
        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}

// ── Dashboard ─────────────────────────────────────────
[Authorize(Roles = "PropertyManager")]
public class DashboardController : Controller
{
    private readonly PropertyLeasingDbContext _db;

    public DashboardController(PropertyLeasingDbContext db)
    {
        _db = db;
    }

    // GET /Dashboard
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            TotalProperties     = await _db.Properties.CountAsync(),
            TotalUnits          = await _db.Units.CountAsync(),
            AvailableUnits      = await _db.Units.CountAsync(u => u.AvailabilityStatus == "Available"),
            OccupiedUnits       = await _db.Units.CountAsync(u => u.AvailabilityStatus == "Occupied"),
            PendingApplications = await _db.LeaseApplications.CountAsync(a => a.Status == "Pending" || a.Status == "Screening"),
            ActiveLeases        = await _db.Leases.CountAsync(l => l.Status == "Active"),
            OpenMaintenanceRequests = await _db.MaintenanceRequests
                .CountAsync(r => r.Status == "Submitted" || r.Status == "Assigned" || r.Status == "InProgress"),
            OverduePayments     = await _db.PaymentRecords
                .CountAsync(p => p.PaymentStatus == "Overdue"),

            PropertyOccupancy = await _db.Properties
                .Include(p => p.Units)
                .Select(p => new PropertyOccupancyViewModel
                {
                    PropertyName  = p.Name,
                    TotalUnits    = p.Units.Count,
                    OccupiedUnits = p.Units.Count(u => u.AvailabilityStatus == "Occupied"),
                    OccupancyRate = p.Units.Count == 0 ? 0 :
                        Math.Round((double)p.Units.Count(u => u.AvailabilityStatus == "Occupied") / p.Units.Count * 100, 1)
                })
                .ToListAsync(),

            RecentMaintenance = await _db.MaintenanceRequests
                .Include(r => r.Unit).ThenInclude(u => u.Property)
                .Include(r => r.Tenant)
                .OrderByDescending(r => r.SubmittedAt)
                .Take(5)
                .Select(r => new MaintenanceListViewModel
                {
                    RequestId    = r.RequestId,
                    Title        = r.Title,
                    Status       = r.Status,
                    Priority     = r.Priority,
                    TicketNumber = r.TicketNumber,
                    UnitNumber   = r.Unit.UnitNumber,
                    PropertyName = r.Unit.Property.Name,
                    TenantName   = r.Tenant.FullName,
                    SubmittedAt  = r.SubmittedAt
                })
                .ToListAsync(),

            RecentApplications = await _db.LeaseApplications
                .Include(a => a.Unit).ThenInclude(u => u.Property)
                .Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new LeaseApplicationListViewModel
                {
                    ApplicationId = a.ApplicationId,
                    TenantName    = a.User.FullName,
                    UnitNumber    = a.Unit.UnitNumber,
                    PropertyName  = a.Unit.Property.Name,
                    Status        = a.Status,
                    CreatedAt     = a.CreatedAt
                })
                .ToListAsync()
        };

        return View(model);
    }
}

// ── Payments ──────────────────────────────────────────
[Authorize(Roles = "PropertyManager,Tenant")]
public class PaymentsController : Controller
{
    private readonly PropertyLeasingDbContext _db;
    private readonly UserManager<AppUser>     _userManager;
    private readonly NotificationService      _notifier;

    public PaymentsController(
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

        var appUser = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identity.Id)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);

        if (appUser != null && appUser.IdentityUserId != identity.Id)
        {
            appUser.IdentityUserId = identity.Id;
            await _db.SaveChangesAsync();
        }

        return appUser;
    }

    // Auto-fix all payment statuses on every page load
    private async Task UpdatePaymentStatusesAsync()
    {
        var today = DateTime.Today;

        // ── 1. Fix properties with missing grace/late-fee settings ──────────
        var propsToFix = await _db.Properties
            .Where(p => p.GracePeriodDays == 0 || p.LateFeePercent == 0)
            .ToListAsync();
        foreach (var prop in propsToFix)
        {
            if (prop.GracePeriodDays == 0) prop.GracePeriodDays = 5;
            if (prop.LateFeePercent  == 0) prop.LateFeePercent  = 5;
        }
        if (propsToFix.Any()) await _db.SaveChangesAsync();

        // ── 2. Load all non-paid records ─────────────────────────────────────
        var active = await _db.PaymentRecords
            .Include(p => p.Lease)
                .ThenInclude(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Where(p => p.PaymentStatus == "Upcoming" ||
                        p.PaymentStatus == "Unpaid"   ||
                        p.PaymentStatus == "Pending")
            .ToListAsync();

        bool changed = false;

        foreach (var p in active)
        {
            // Legacy "Pending" → "Unpaid"
            if (p.PaymentStatus == "Pending")
            {
                p.PaymentStatus = "Unpaid";
                changed = true;
                continue;
            }

            // Future "Unpaid" (old data) → "Upcoming"
            if (p.PaymentStatus == "Unpaid" && p.DueDate > today.AddDays(2))
            {
                p.PaymentStatus = "Upcoming";
                changed = true;
                continue;
            }

            // "Upcoming" → "Unpaid" when due date is within 2 days
            if (p.PaymentStatus == "Upcoming" && p.DueDate <= today.AddDays(2))
            {
                p.PaymentStatus = "Unpaid";
                changed = true;
                continue;
            }

            // "Unpaid" → "Overdue" after grace period
            if (p.PaymentStatus == "Unpaid")
            {
                int grace = p.Lease.Application.Unit.Property.GracePeriodDays;
                if (p.DueDate.AddDays(grace) < today)
                {
                    p.PaymentStatus = "Overdue";
                    p.LateFee = Math.Round(p.AmountDue * p.Lease.Application.Unit.Property.LateFeePercent / 100, 2);
                    changed = true;
                }
            }
        }

        if (changed) await _db.SaveChangesAsync();
    }

    // GET /Payments
    public async Task<IActionResult> Index(string? status)
    {
        await UpdatePaymentStatusesAsync();

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var baseQuery = _db.PaymentRecords
            .Include(p => p.Lease)
                .ThenInclude(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(p => p.Lease.Application.User)
            .AsQueryable();

        if (appUser.Role == "Tenant")
            baseQuery = baseQuery.Where(p => p.Lease.Application.UserId == appUser.UserId);

        // Counts from full dataset (before status filter) for tab badges
        var allStatuses = await baseQuery.Select(p => p.PaymentStatus).ToListAsync();
        ViewBag.TabCounts = new Dictionary<string, int>
        {
            ["All"]      = allStatuses.Count,
            ["Unpaid"]   = allStatuses.Count(s => s == "Unpaid" || s == "Pending"),
            ["Overdue"]  = allStatuses.Count(s => s == "Overdue"),
            ["Upcoming"] = allStatuses.Count(s => s == "Upcoming"),
            ["Paid"]     = allStatuses.Count(s => s == "Paid"),
        };

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.PaymentStatus == status || (status == "Unpaid" && p.PaymentStatus == "Pending"));

        var rawPayments = await query.OrderByDescending(p => p.DueDate).ToListAsync();

        // Group by lease to compute installment numbers
        var grouped = rawPayments
            .GroupBy(p => p.LeaseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.DueDate).ToList()
            );

        var payments = rawPayments.Select(p =>
        {
            var group = grouped[p.LeaseId];
            var idx   = group.IndexOf(p);
            return new PaymentListViewModel
            {
                PaymentId         = p.PaymentId,
                LeaseId           = p.LeaseId,
                UnitNumber        = p.Lease.Application.Unit.UnitNumber,
                PropertyName      = p.Lease.Application.Unit.Property.Name,
                TenantName        = p.Lease.Application.User.FullName,
                AmountDue         = p.AmountDue,
                AmountPaid        = p.AmountPaid,
                LateFee           = p.LateFee,
                DueDate           = p.DueDate,
                PaidDate          = p.PaidDate,
                PaymentStatus     = p.PaymentStatus,
                Notes             = p.Notes,
                PaymentPlanType   = p.Lease.PaymentPlanType,
                InstallmentNum    = idx + 1,
                TotalInstallments = group.Count
            };
        }).ToList();

        ViewBag.Status   = status;
        ViewBag.IsManager = appUser.Role == "PropertyManager";
        return View(payments);
    }

    // GET /Payments/Pay/{paymentId} — Tenant pays a single installment
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Pay(int paymentId)
    {
        await UpdatePaymentStatusesAsync();

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var payment = await _db.PaymentRecords
            .Include(p => p.Lease)
                .ThenInclude(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment == null) return NotFound();
        if (payment.Lease.Application.UserId != appUser.UserId) return Forbid();
        if (payment.PaymentStatus == "Paid")
        {
            TempData["Error"] = "This installment has already been paid.";
            return RedirectToAction("Index");
        }

        var ordered = await _db.PaymentRecords
            .Where(p => p.LeaseId == payment.LeaseId)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        var vm = new TenantPayViewModel
        {
            PaymentId         = payment.PaymentId,
            LeaseId           = payment.LeaseId,
            UnitNumber        = payment.Lease.Application.Unit.UnitNumber,
            PropertyName      = payment.Lease.Application.Unit.Property.Name,
            InstallmentNum    = ordered.FindIndex(p => p.PaymentId == paymentId) + 1,
            TotalInstallments = ordered.Count,
            AmountDue         = payment.AmountDue,
            LateFee           = payment.LateFee,
            DueDate           = payment.DueDate
        };

        return View(vm);
    }

    // POST /Payments/TenantPay
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TenantPay(TenantPayViewModel vm)
    {
        if (!ModelState.IsValid)
            return View("Pay", vm);

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var payment = await _db.PaymentRecords
            .Include(p => p.Lease)
                .ThenInclude(l => l.Application)
                .ThenInclude(a => a.Unit)
            .FirstOrDefaultAsync(p => p.PaymentId == vm.PaymentId);

        if (payment == null) return NotFound();
        if (payment.Lease.Application.UserId != appUser.UserId) return Forbid();
        if (payment.PaymentStatus == "Paid")
        {
            TempData["Error"] = "This installment has already been paid.";
            return RedirectToAction("Index");
        }

        payment.AmountPaid    = vm.TotalAmount;
        payment.PaidDate      = DateTime.Now;
        payment.PaymentStatus = "Paid";
        payment.LateFee       = vm.LateFee;
        payment.Notes         = "Paid online by tenant.";

        await _db.SaveChangesAsync();

        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"{appUser.FullName} paid installment {vm.InstallmentNum}/{vm.TotalInstallments} (BD {vm.TotalAmount:N2}) for unit {payment.Lease.Application.Unit.UnitNumber}.",
                "PaymentReminder");

        TempData["Success"] = $"Payment of BD {vm.TotalAmount:N2} completed successfully!";
        return RedirectToAction("Index");
    }

    // GET /Payments/SelectPlan/{leaseId}
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> SelectPlan(int leaseId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "PendingPayment")
        {
            TempData["Error"] = "This lease does not require payment at this time.";
            return RedirectToAction("Index", "LeaseApplications", new { tab = "leases" });
        }

        int totalMonths = (int)Math.Ceiling((lease.LeaseEndDate - lease.LeaseStartDate).TotalDays / 30.44);
        return View(new SelectPlanViewModel
        {
            LeaseId         = lease.LeaseId,
            UnitNumber      = lease.Application.Unit.UnitNumber,
            PropertyName    = lease.Application.Unit.Property.Name,
            LeaseStartDate  = lease.LeaseStartDate,
            LeaseEndDate    = lease.LeaseEndDate,
            MonthlyRent     = lease.MonthlyRent,
            SecurityDeposit = lease.SecurityDeposit,
            TotalMonths     = totalMonths,
            TotalAmount     = lease.MonthlyRent * totalMonths
        });
    }

    // POST /Payments/SelectPlan
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectPlan(SelectPlanViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var lease = await _db.Leases
                .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(l => l.LeaseId == vm.LeaseId);
            if (lease != null)
            {
                int m = (int)Math.Ceiling((lease.LeaseEndDate - lease.LeaseStartDate).TotalDays / 30.44);
                vm.TotalMonths = m; vm.TotalAmount = lease.MonthlyRent * m;
                vm.MonthlyRent = lease.MonthlyRent; vm.SecurityDeposit = lease.SecurityDeposit;
            }
            return View(vm);
        }
        return RedirectToAction("Checkout", new { leaseId = vm.LeaseId, plan = vm.SelectedPlan });
    }

    // GET /Payments/Checkout
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Checkout(int leaseId, string plan)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "PendingPayment")
            return RedirectToAction("Index", "LeaseApplications", new { tab = "leases" });

        int totalMonths = (int)Math.Ceiling((lease.LeaseEndDate - lease.LeaseStartDate).TotalDays / 30.44);
        return View(new CheckoutViewModel
        {
            LeaseId         = lease.LeaseId,
            UnitNumber      = lease.Application.Unit.UnitNumber,
            PropertyName    = lease.Application.Unit.Property.Name,
            PlanType        = plan,
            MonthlyRent     = lease.MonthlyRent,
            TotalMonths     = totalMonths,
            AmountToPay     = plan == "Full" ? lease.MonthlyRent * totalMonths : lease.MonthlyRent,
            PlanDescription = plan == "Full"
                ? $"Full payment for {totalMonths} months"
                : $"Installment 1 of {totalMonths}"
        });
    }

    // POST /Payments/ProcessPayment
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(CheckoutViewModel vm)
    {
        if (!ModelState.IsValid) return View("Checkout", vm);

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var lease = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(l => l.LeaseId == vm.LeaseId);

        if (lease == null) return NotFound();
        if (lease.Application.UserId != appUser.UserId) return Forbid();
        if (lease.Status != "PendingPayment")
        {
            TempData["Error"] = "This lease has already been processed.";
            return RedirectToAction("Index", "LeaseApplications", new { tab = "leases" });
        }

        var now = DateTime.Now;
        int totalMonths = (int)Math.Ceiling((lease.LeaseEndDate - lease.LeaseStartDate).TotalDays / 30.44);

        if (vm.PlanType == "Full")
        {
            _db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = lease.LeaseId,
                AmountDue     = lease.MonthlyRent * totalMonths,
                AmountPaid    = lease.MonthlyRent * totalMonths,
                DueDate       = lease.LeaseStartDate,
                PaidDate      = now,
                PaymentStatus = "Paid",
                Notes         = "Full payment at lease activation."
            });
        }
        else
        {
            for (int i = 0; i < totalMonths; i++)
            {
                _db.PaymentRecords.Add(new PaymentRecord
                {
                    LeaseId       = lease.LeaseId,
                    AmountDue     = lease.MonthlyRent,
                    AmountPaid    = i == 0 ? lease.MonthlyRent : null,
                    DueDate       = lease.LeaseStartDate.AddMonths(i),
                    PaidDate      = i == 0 ? now : null,
                    PaymentStatus = i == 0 ? "Paid" : "Upcoming",
                    Notes         = i == 0 ? "First installment paid at activation." : null
                });
            }
        }

        lease.Status          = "Active";
        lease.PaymentPlanType = vm.PlanType;
        lease.Application.Unit.AvailabilityStatus = "Occupied";

        _db.LeaseLogs.Add(new LeaseLog
        {
            LeaseId         = lease.LeaseId,
            Status          = "Active",
            ChangedByUserId = appUser.UserId,
            Notes           = $"Lease activated after {vm.PlanType} payment.",
            CreatedAt       = now
        });

        await _db.SaveChangesAsync();

        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"{appUser.FullName} completed {vm.PlanType.ToLower()} payment for unit {lease.Application.Unit.UnitNumber}. Lease is now Active.",
                "PaymentReminder");

        TempData["Success"] = vm.PlanType == "Full"
            ? "Full payment completed! Your lease is now Active."
            : "First installment paid! Your lease is now Active. Next installment will be available next month.";

        return RedirectToAction("Index", "LeaseApplications", new { tab = "leases" });
    }

    // POST /Payments/RecordPayment — Manager only
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int paymentId, decimal amountPaid, string? notes)
    {
        var payment = await _db.PaymentRecords
            .Include(p => p.Lease).ThenInclude(l => l.Application).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (payment == null) return NotFound();

        payment.AmountPaid    = amountPaid;
        payment.PaidDate      = DateTime.Now;
        payment.Notes         = notes;
        payment.PaymentStatus = "Paid";
        payment.LateFee       = null;

        await _db.SaveChangesAsync();

        await _notifier.SendAsync(payment.Lease.Application.UserId,
            $"Your installment payment of BD {amountPaid:N2} for unit {payment.Lease.Application.Unit?.UnitNumber} has been recorded.",
            "PaymentReminder");

        TempData["Success"] = "Payment recorded successfully.";
        return RedirectToAction("Index");
    }
}
