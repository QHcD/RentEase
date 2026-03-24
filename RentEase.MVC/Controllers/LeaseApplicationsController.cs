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

    // Helper: get the app User from the logged-in identity user
    private async Task<User?> GetAppUserAsync()
    {
        var identity = await _userManager.GetUserAsync(User);
        if (identity == null) return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identity.Id);
    }

    // GET /LeaseApplications — tenant sees their own, manager sees all
    public async Task<IActionResult> Index(string? status)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var query = _db.LeaseApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.User)
            .AsQueryable();

        // Tenants only see their own applications
        if (appUser.Role == "Tenant")
            query = query.Where(a => a.UserId == appUser.UserId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        var apps = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new LeaseApplicationListViewModel
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
            })
            .ToListAsync();

        ViewBag.Status = status;
        return View(apps);
    }

    // GET /LeaseApplications/Apply/{unitId}
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Apply(int unitId)
    {
        var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == unitId);
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
        if (!ModelState.IsValid) return View(model);

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        // Check no pending/approved application already exists for this unit
        var existing = await _db.LeaseApplications
            .AnyAsync(a => a.UnitId == model.UnitId && a.UserId == appUser.UserId
                        && (a.Status == "Pending" || a.Status == "Screening" || a.Status == "Approved"));
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

        // Notify tenant
        await _notifier.SendAsync(appUser.UserId,
            "Your lease application has been submitted and is under review.",
            "LeaseUpdate");

        // Notify all managers
        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"New lease application from {appUser.FullName} for unit {model.UnitNumber}.",
                "LeaseUpdate");

        TempData["Success"] = $"Application submitted! Your ticket is being reviewed.";
        return RedirectToAction("Index");
    }

    // POST /LeaseApplications/UpdateStatus — Manager only
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int applicationId, string newStatus)
    {
        var application = await _db.LeaseApplications
            .Include(a => a.Unit)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (application == null) return NotFound();

        application.Status = newStatus;

        // If approved → create Lease and mark unit as Occupied
        if (newStatus == "Approved")
        {
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

            // Generate first payment record
            _db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = lease.LeaseId,
                AmountDue     = lease.MonthlyRent,
                DueDate       = lease.LeaseStartDate,
                PaymentStatus = "Pending"
            });
        }

        await _db.SaveChangesAsync();

        // Notify tenant
        await _notifier.SendAsync(application.UserId,
            $"Your lease application for unit {application.Unit.UnitNumber} has been {newStatus.ToLower()}.",
            "LeaseUpdate");

        TempData["Success"] = $"Application status updated to {newStatus}.";
        return RedirectToAction("Index");
    }
}
