using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
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
                .CountAsync(p => p.PaymentStatus == "Pending" && p.DueDate < DateTime.Now),

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

    public PaymentsController(PropertyLeasingDbContext db, UserManager<AppUser> userManager)
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

    // GET /Payments
    public async Task<IActionResult> Index(string? status)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var query = _db.PaymentRecords
            .Include(p => p.Lease)
                .ThenInclude(l => l.Application)
                .ThenInclude(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(p => p.Lease.Application.User)
            .AsQueryable();

        // Tenants only see their own payments
        if (appUser.Role == "Tenant")
            query = query.Where(p => p.Lease.Application.UserId == appUser.UserId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.PaymentStatus == status);

        var payments = await query
            .OrderByDescending(p => p.DueDate)
            .Select(p => new PaymentListViewModel
            {
                PaymentId     = p.PaymentId,
                UnitNumber    = p.Lease.Application.Unit.UnitNumber,
                PropertyName  = p.Lease.Application.Unit.Property.Name,
                TenantName    = p.Lease.Application.User.FullName,
                AmountDue     = p.AmountDue,
                AmountPaid    = p.AmountPaid,
                DueDate       = p.DueDate,
                PaidDate      = p.PaidDate,
                PaymentStatus = p.PaymentStatus,
                Notes         = p.Notes
            })
            .ToListAsync();

        ViewBag.Status = status;
        return View(payments);
    }

    // POST /Payments/RecordPayment — Manager only
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int paymentId, decimal amountPaid, string? notes)
    {
        var payment = await _db.PaymentRecords.FindAsync(paymentId);
        if (payment == null) return NotFound();

        payment.AmountPaid    = amountPaid;
        payment.PaidDate      = DateTime.Now;
        payment.Notes         = notes;
        payment.PaymentStatus = amountPaid >= payment.AmountDue ? "Paid" : "PartiallyPaid";

        await _db.SaveChangesAsync();
        TempData["Success"] = "Payment recorded successfully.";
        return RedirectToAction("Index");
    }
}
