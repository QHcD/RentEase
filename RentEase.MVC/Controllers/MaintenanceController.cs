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
public class MaintenanceController : Controller
{
    private readonly PropertyLeasingDbContext _db;
    private readonly UserManager<AppUser>     _userManager;
    private readonly NotificationService      _notifier;
    private readonly EmailService             _emailService;

    public MaintenanceController(
        PropertyLeasingDbContext db,
        UserManager<AppUser> userManager,
        NotificationService notifier,
        EmailService emailService)
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
        return await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identity.Id);
    }

    // GET /Maintenance
    public async Task<IActionResult> Index(string? status)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var baseQuery = _db.MaintenanceRequests
            .Include(r => r.Unit).ThenInclude(u => u.Property)
            .Include(r => r.Tenant)
            .Include(r => r.AssignedStaff)
            .AsQueryable();

        // Tenants only see their own requests
        if (appUser.Role == "Tenant")
            baseQuery = baseQuery.Where(r => r.TenantUserId == appUser.UserId);

        // Staff only see requests assigned to them
        if (appUser.Role == "MaintenanceStaff")
            baseQuery = baseQuery.Where(r => r.AssignedStaffId == appUser.UserId);

        // Compute tab counts BEFORE status filter
        var allStatuses = await baseQuery.Select(r => r.Status).ToListAsync();
        ViewBag.TabCounts = new Dictionary<string, int>
        {
            ["All"]        = allStatuses.Count,
            ["Submitted"]  = allStatuses.Count(s => s == "Submitted"),
            ["Assigned"]   = allStatuses.Count(s => s == "Assigned"),
            ["InProgress"] = allStatuses.Count(s => s == "InProgress"),
            ["Resolved"]   = allStatuses.Count(s => s == "Resolved"),
        };

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        var requests = await query
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new MaintenanceListViewModel
            {
                RequestId     = r.RequestId,
                Title         = r.Title,
                Description   = r.Description,
                RequestType   = r.RequestType,
                Priority      = r.Priority,
                Status        = r.Status,
                TicketNumber  = r.TicketNumber,
                UnitNumber    = r.Unit.UnitNumber,
                PropertyName  = r.Unit.Property.Name,
                TenantName    = r.Tenant.FullName,
                AssignedStaff = r.AssignedStaff != null ? r.AssignedStaff.FullName : null,
                SubmittedAt   = r.SubmittedAt,
                ResolvedAt    = r.ResolvedAt
            })
            .ToListAsync();

        ViewBag.CurrentStatus = status;
        ViewBag.Role          = appUser.Role;
        return View(requests);
    }

    // GET /Maintenance/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        var request = await _db.MaintenanceRequests
            .Include(r => r.Unit).ThenInclude(u => u.Property)
            .Include(r => r.Tenant)
            .Include(r => r.AssignedStaff)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.RequestId == id);

        if (request == null) return NotFound();

        // Tenants can only see their own
        if (appUser.Role == "Tenant" && request.TenantUserId != appUser.UserId)
            return Forbid();

        // Staff can only see assigned
        if (appUser.Role == "MaintenanceStaff" && request.AssignedStaffId != appUser.UserId)
            return Forbid();

        // Build history with changer names
        var changerIds = request.StatusHistory
            .Where(h => h.ChangedByUserId.HasValue)
            .Select(h => h.ChangedByUserId!.Value)
            .Distinct()
            .ToList();

        var changers = await _db.Users
            .Where(u => changerIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName);

        var staffList = appUser.Role != "Tenant"
            ? await _db.Users
                .Include(u => u.StaffProfile)
                .Where(u => u.Role == "MaintenanceStaff")
                .Select(u => new StaffSelectItem
                {
                    UserId             = u.UserId,
                    FullName           = u.FullName,
                    SkillProfile       = u.StaffProfile != null ? u.StaffProfile.SkillProfile       : null,
                    AvailabilityStatus = u.StaffProfile != null ? u.StaffProfile.AvailabilityStatus : null
                })
                .ToListAsync()
            : new List<StaffSelectItem>();

        var vm = new MaintenanceDetailViewModel
        {
            RequestId       = request.RequestId,
            TicketNumber    = request.TicketNumber ?? "",
            Title           = request.Title,
            Description     = request.Description,
            RequestType     = request.RequestType,
            Priority        = request.Priority,
            Status          = request.Status,
            UnitNumber      = request.Unit.UnitNumber,
            PropertyName    = request.Unit.Property.Name,
            TenantName      = request.Tenant.FullName,
            TenantEmail     = request.Tenant.Email,
            AssignedStaff   = request.AssignedStaff?.FullName,
            AssignedStaffId = request.AssignedStaffId,
            SubmittedAt     = request.SubmittedAt,
            ResolvedAt      = request.ResolvedAt,
            ResolutionNotes = request.ResolutionNotes,
            ImagePath           = request.ImagePath,
            ResolutionImagePath = request.ResolutionImagePath,
            StaffList           = staffList,
            History         = request.StatusHistory
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new StatusHistoryViewModel
                {
                    OldStatus     = h.OldStatus,
                    NewStatus     = h.NewStatus,
                    Notes         = h.Notes,
                    ChangedAt     = h.ChangedAt,
                    ChangedByName = h.ChangedByUserId.HasValue && changers.ContainsKey(h.ChangedByUserId.Value)
                                    ? changers[h.ChangedByUserId.Value]
                                    : "System"
                }).ToList()
        };

        ViewBag.Role = appUser.Role;
        return View(vm);
    }

    // GET /Maintenance/Submit
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Submit(int? unitId)
    {
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        // Load ALL active lease units for this tenant
        var activeLeases = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .Where(l => l.Application.UserId == appUser.UserId && l.Status == "Active")
            .ToListAsync();

        var availableUnits = activeLeases
            .Select(l => new UnitSelectOption
            {
                UnitId       = l.Application.Unit.UnitId,
                UnitNumber   = l.Application.Unit.UnitNumber,
                PropertyName = l.Application.Unit.Property.Name
            })
            .DistinctBy(u => u.UnitId)
            .ToList();

        // Determine pre-selected unit
        Unit? unit = null;
        if (unitId.HasValue)
        {
            unit = await _db.Units.Include(u => u.Property)
                       .FirstOrDefaultAsync(u => u.UnitId == unitId);
        }
        else if (availableUnits.Count == 1)
        {
            // Auto-select if only one unit
            var single = availableUnits[0];
            unit = await _db.Units.Include(u => u.Property)
                       .FirstOrDefaultAsync(u => u.UnitId == single.UnitId);
        }

        return View(new CreateMaintenanceViewModel
        {
            UnitId         = unit?.UnitId ?? (availableUnits.Count == 1 ? availableUnits[0].UnitId : 0),
            UnitNumber     = unit?.UnitNumber ?? "",
            PropertyName   = unit?.Property?.Name ?? "",
            AvailableUnits = availableUnits
        });
    }

    // POST /Maintenance/Submit
    [Authorize(Roles = "Tenant")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(CreateMaintenanceViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        // If unit not filled yet, reload with units list
        if (model.UnitId == 0)
        {
            var activeLeases = await _db.Leases
                .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
                .Where(l => l.Application.UserId == appUser.UserId && l.Status == "Active")
                .ToListAsync();
            model.AvailableUnits = activeLeases
                .Select(l => new UnitSelectOption
                {
                    UnitId       = l.Application.Unit.UnitId,
                    UnitNumber   = l.Application.Unit.UnitNumber,
                    PropertyName = l.Application.Unit.Property.Name
                })
                .DistinctBy(u => u.UnitId)
                .ToList();
            ModelState.AddModelError("UnitId", "Please select a unit.");
            return View(model);
        }

        // Populate UnitNumber/PropertyName from DB (in case hidden fields weren't filled)
        if (string.IsNullOrEmpty(model.UnitNumber))
        {
            var u = await _db.Units.Include(u => u.Property)
                        .FirstOrDefaultAsync(u => u.UnitId == model.UnitId);
            if (u != null)
            {
                model.UnitNumber   = u.UnitNumber;
                model.PropertyName = u.Property.Name;
            }
        }

        var ticketNumber = $"TKT-{DateTime.Now:yyyy}-{new Random().Next(1000, 9999)}";

        // Handle image upload
        string? imagePath = null;
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            try
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var ext     = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
                if (allowed.Contains(ext))
                {
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "maintenance");
                    Directory.CreateDirectory(uploadsDir);
                    var fileName = $"{ticketNumber.Replace("/", "-")}_{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsDir, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await model.ImageFile.CopyToAsync(stream);
                    imagePath = $"/uploads/maintenance/{fileName}";
                }
            }
            catch { /* image save failure is non-fatal */ }
        }

        var request = new MaintenanceRequest
        {
            UnitId       = model.UnitId,
            TenantUserId = appUser.UserId,
            Title        = model.Title,
            Description  = model.Description,
            RequestType  = model.RequestType,
            Priority     = model.Priority,
            Status       = "Submitted",
            TicketNumber = ticketNumber,
            SubmittedAt  = DateTime.Now,
            ImagePath    = imagePath
        };

        _db.MaintenanceRequests.Add(request);
        await _db.SaveChangesAsync();

        // Notify managers
        var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
        foreach (var mgr in managers)
            await _notifier.SendAsync(mgr.UserId,
                $"New maintenance request: {model.Title} — Ticket: {ticketNumber}",
                "MaintenanceUpdate");

        // Send confirmation email to tenant
        if (!string.IsNullOrWhiteSpace(appUser.Email))
        {
            try
            {
                await _emailService.SendMaintenanceSubmittedAsync(
                    toEmail:      appUser.Email,
                    toName:       appUser.FullName,
                    ticketNumber: ticketNumber,
                    title:        model.Title,
                    requestType:  model.RequestType ?? "General",
                    priority:     model.Priority,
                    unitNumber:   model.UnitNumber,
                    propertyName: model.PropertyName);
            }
            catch { /* email failure must not block the flow */ }
        }

        TempData["Success"] = $"Request submitted! Your ticket number is <strong>{ticketNumber}</strong>.";
        return RedirectToAction("Index");
    }

    // GET /Maintenance/Update/{id} — Manager/Staff
    [Authorize(Roles = "PropertyManager,MaintenanceStaff")]
    public async Task<IActionResult> Update(int id)
    {
        var request = await _db.MaintenanceRequests.FindAsync(id);
        if (request == null) return NotFound();

        var staffList = await _db.Users
            .Include(u => u.StaffProfile)
            .Where(u => u.Role == "MaintenanceStaff")
            .Select(u => new StaffSelectItem
            {
                UserId             = u.UserId,
                FullName           = u.FullName,
                SkillProfile       = u.StaffProfile != null ? u.StaffProfile.SkillProfile       : null,
                AvailabilityStatus = u.StaffProfile != null ? u.StaffProfile.AvailabilityStatus : null
            })
            .ToListAsync();

        return View(new UpdateMaintenanceViewModel
        {
            RequestId       = request.RequestId,
            Title           = request.Title,
            Description     = request.Description,
            RequestType     = request.RequestType,
            ImagePath       = request.ImagePath,
            CurrentStatus   = request.Status,
            NewStatus       = request.Status,
            AssignedStaffId = request.AssignedStaffId,
            StaffList       = staffList
        });
    }

    // POST /Maintenance/Update
    [Authorize(Roles = "PropertyManager,MaintenanceStaff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateMaintenanceViewModel model)
    {
        var request = await _db.MaintenanceRequests
            .Include(r => r.Tenant)
            .Include(r => r.Unit).ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(r => r.RequestId == model.RequestId);

        if (request == null) return NotFound();

        var appUser = await GetAppUserAsync();

        // ── LOCK: Resolved and Closed requests cannot be changed ──────────────
        if (request.Status == "Resolved" || request.Status == "Closed")
        {
            TempData["Error"] = $"This request is already {request.Status} and cannot be modified.";
            return RedirectToAction("Details", new { id = request.RequestId });
        }

        // Staff can only set InProgress / Resolved — not reassign status to Submitted/Assigned/Closed
        if (appUser?.Role == "MaintenanceStaff")
        {
            var allowedForStaff = new[] { "InProgress", "Resolved" };
            if (!allowedForStaff.Contains(model.NewStatus))
                model.NewStatus = request.Status; // keep unchanged if invalid
        }

        var oldStatus = request.Status;

        // Save status history
        _db.MaintenanceStatusHistories.Add(new MaintenanceStatusHistory
        {
            RequestId       = request.RequestId,
            OldStatus       = request.Status,
            NewStatus       = model.NewStatus,
            Notes           = model.Notes,
            ChangedAt       = DateTime.Now,
            ChangedByUserId = appUser?.UserId
        });

        request.Status = model.NewStatus;

        if (model.AssignedStaffId.HasValue && appUser?.Role == "PropertyManager")
            request.AssignedStaffId = model.AssignedStaffId;

        if (model.NewStatus == "Resolved")
        {
            request.ResolvedAt      = DateTime.Now;
            request.ResolutionNotes = model.Notes;

            // Save resolution proof photo if uploaded
            if (model.ResolutionImageFile != null && model.ResolutionImageFile.Length > 0)
            {
                try
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                    var ext     = Path.GetExtension(model.ResolutionImageFile.FileName).ToLowerInvariant();
                    if (allowed.Contains(ext))
                    {
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "maintenance");
                        Directory.CreateDirectory(uploadsDir);
                        var fileName = $"RESOLVED-{(request.TicketNumber ?? request.RequestId.ToString()).Replace("/","-")}_{Guid.NewGuid():N}{ext}";
                        var filePath = Path.Combine(uploadsDir, fileName);
                        using var stream = new FileStream(filePath, FileMode.Create);
                        await model.ResolutionImageFile.CopyToAsync(stream);
                        request.ResolutionImagePath = $"/uploads/maintenance/{fileName}";
                    }
                }
                catch { /* non-fatal */ }
            }
        }

        await _db.SaveChangesAsync();

        // ── Rich notification messages ─────────────────────────────────────────
        var updaterName  = appUser?.FullName ?? "Staff";
        var notesSnippet = !string.IsNullOrWhiteSpace(model.Notes)
            ? $" — \"{model.Notes.Trim().Substring(0, Math.Min(model.Notes.Trim().Length, 80))}\""
            : "";

        // Notify tenant (in-app) — show old → new transition + notes snippet
        var tenantMsg = model.NewStatus == "Resolved"
            ? $"✅ Your request \"{request.Title}\" has been RESOLVED{notesSnippet}"
            : $"🔧 Request \"{request.Title}\": {oldStatus} → {model.NewStatus}{notesSnippet}";
        await _notifier.SendAsync(request.TenantUserId, tenantMsg, "MaintenanceUpdate");

        // Notify manager when staff changes status
        if (appUser?.Role == "MaintenanceStaff")
        {
            var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
            foreach (var mgr in managers)
                await _notifier.SendAsync(mgr.UserId,
                    $"🔧 [{request.TicketNumber}] \"{request.Title}\": {oldStatus} → {model.NewStatus} by {updaterName}{notesSnippet}",
                    "MaintenanceUpdate");
        }

        // Notify assigned staff if newly assigned
        if (model.AssignedStaffId.HasValue && appUser?.Role == "PropertyManager")
            await _notifier.SendAsync(model.AssignedStaffId.Value,
                $"📋 You have been assigned to \"{request.Title}\" (Ticket: {request.TicketNumber}). Status: {model.NewStatus}",
                "MaintenanceUpdate");

        // Send status-change email to tenant
        if (!string.IsNullOrWhiteSpace(request.Tenant?.Email))
        {
            try
            {
                await _emailService.SendMaintenanceStatusChangedAsync(
                    toEmail:      request.Tenant.Email,
                    toName:       request.Tenant.FullName,
                    ticketNumber: request.TicketNumber ?? "",
                    title:        request.Title,
                    unitNumber:   request.Unit?.UnitNumber ?? "",
                    newStatus:    model.NewStatus,
                    notes:        model.Notes);
            }
            catch { /* email failure must not block the flow */ }
        }

        TempData["Success"] = "Maintenance request updated successfully.";
        return RedirectToAction("Details", new { id = request.RequestId });
    }
}
