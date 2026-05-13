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
    private static readonly HashSet<string> AllowedRequestTypes =
        new(new[] { "Plumbing", "Electrical", "HVAC", "Carpentry", "General" }, StringComparer.OrdinalIgnoreCase);

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

    private async Task PopulateTenantUnitsAsync(CreateMaintenanceViewModel model, int userId)
    {
        var activeLeases = await _db.Leases
            .Include(l => l.Application).ThenInclude(a => a.Unit).ThenInclude(u => u.Property)
            .Where(l => l.Application.UserId == userId && l.Status == "Active")
            .ToListAsync();

        model.AvailableUnits = activeLeases
            .Select(l => new UnitSelectOption
            {
                UnitId = l.Application.Unit.UnitId,
                UnitNumber = l.Application.Unit.UnitNumber,
                PropertyName = l.Application.Unit.Property.Name
            })
            .DistinctBy(u => u.UnitId)
            .ToList();
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

        // MaintenanceStaff: if the request belongs to a different staff member, show
        // an informational message on the same page instead of a hard 403.
        if (appUser.Role == "MaintenanceStaff" && request.AssignedStaffId != appUser.UserId)
        {
            ViewBag.Role            = appUser.Role;
            ViewBag.NotAssigned     = true;
            ViewBag.AssignedToName  = request.AssignedStaff?.FullName; // null = not yet assigned
            return View(new MaintenanceDetailViewModel
            {
                RequestId    = request.RequestId,
                TicketNumber = request.TicketNumber ?? "",
                Title        = request.Title,
                RequestType  = request.RequestType,
                Priority     = request.Priority,
                Status       = request.Status,
                UnitNumber   = request.Unit.UnitNumber,
                PropertyName = request.Unit.Property.Name,
                TenantName   = request.Tenant.FullName,
                SubmittedAt  = request.SubmittedAt,
                History      = new List<StatusHistoryViewModel>(),
                StaffList    = new List<StaffSelectItem>()
            });
        }

        // Build history with changer names
        var changerIds = request.StatusHistory
            .Where(h => h.ChangedByUserId.HasValue)
            .Select(h => h.ChangedByUserId!.Value)
            .Distinct()
            .ToList();

        var changers = await _db.Users
            .Where(u => changerIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName);

        // Load all staff then filter by request category.
        // "General" requests are open to every staff member.
        // Any other category only shows staff whose SkillProfile contains that category.
        var allStaff = appUser.Role != "Tenant"
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

        var isGeneralOrUnset = string.IsNullOrEmpty(request.RequestType) ||
                               request.RequestType.Equals("General", StringComparison.OrdinalIgnoreCase);

        var staffList = isGeneralOrUnset
            ? allStaff
            : allStaff
                .Where(s => !string.IsNullOrEmpty(s.SkillProfile) &&
                            s.SkillProfile
                             .Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Any(skill => skill.Trim().Equals(request.RequestType, StringComparison.OrdinalIgnoreCase)))
                .ToList();

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
        var appUser = await GetAppUserAsync();
        if (appUser == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            await PopulateTenantUnitsAsync(model, appUser.UserId);
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.RequestType) || !AllowedRequestTypes.Contains(model.RequestType))
        {
            ModelState.AddModelError(nameof(model.RequestType),
                "Category must be Plumbing, Electrical, HVAC, Carpentry, or General.");
            await PopulateTenantUnitsAsync(model, appUser.UserId);
            return View(model);
        }

        // If unit not filled yet, reload with units list
        if (model.UnitId == 0)
        {
            await PopulateTenantUnitsAsync(model, appUser.UserId);
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

        var now = DateTime.Now;

        // Record "Submitted" in the Status History (shows in the Details timeline)
        _db.MaintenanceStatusHistories.Add(new MaintenanceStatusHistory
        {
            RequestId       = request.RequestId,
            OldStatus       = null,
            NewStatus       = "Submitted",
            Notes           = $"Request submitted by tenant. Category: {model.RequestType}. Priority: {model.Priority}.",
            ChangedAt       = now,
            ChangedByUserId = appUser.UserId
        });

        // Record in the activity log
        _db.MaintenanceRequestLogs.Add(new MaintenanceRequestLog
        {
            RequestId         = request.RequestId,
            Action            = "Submitted",
            Details           = $"Ticket {ticketNumber} submitted for unit {model.UnitNumber}. Category: {model.RequestType}. Priority: {model.Priority}.",
            PerformedByUserId = appUser.UserId,
            PerformedAt       = now
        });
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

    // POST /Maintenance/Update  — form submitted from Details page
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
        if (appUser == null) return Unauthorized();

        // Resolved / Closed requests are locked
        if (request.Status == "Resolved" || request.Status == "Closed")
        {
            TempData["Error"] = $"This request is already {request.Status} and cannot be modified.";
            return RedirectToAction("Details", new { id = request.RequestId });
        }

        var oldStatus          = request.Status;
        var oldAssignedStaffId = request.AssignedStaffId;

        // Manager flow: assigning staff auto-moves Submitted -> Assigned
        if (appUser.Role == "PropertyManager")
        {
            if (model.AssignedStaffId.HasValue)
            {
                request.AssignedStaffId = model.AssignedStaffId.Value;
                model.NewStatus = request.Status == "Submitted" ? "Assigned" : request.Status;
            }
            else
            {
                model.NewStatus = request.Status;
            }
        }
        else if (appUser.Role == "MaintenanceStaff")
        {
            if (request.AssignedStaffId != appUser.UserId)
                return Forbid();

            var isAssignedToInProgress = request.Status == "Assigned"    && model.NewStatus == "InProgress";
            var isInProgressToResolved = request.Status == "InProgress"  && model.NewStatus == "Resolved";
            if (!isAssignedToInProgress && !isInProgressToResolved)
            {
                TempData["Error"] = "Invalid status transition for maintenance staff.";
                return RedirectToAction("Details", new { id = request.RequestId });
            }
        }

        request.Status = model.NewStatus;

        if (oldStatus != request.Status)
        {
            _db.MaintenanceStatusHistories.Add(new MaintenanceStatusHistory
            {
                RequestId       = request.RequestId,
                OldStatus       = oldStatus,
                NewStatus       = request.Status,
                Notes           = model.Notes,
                ChangedAt       = DateTime.Now,
                ChangedByUserId = appUser.UserId
            });
        }

        if (model.NewStatus == "Resolved")
        {
            request.ResolvedAt      = DateTime.Now;
            request.ResolutionNotes = model.Notes;

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
                        var fileName = $"RESOLVED-{(request.TicketNumber ?? request.RequestId.ToString()).Replace("/", "-")}_{Guid.NewGuid():N}{ext}";
                        var filePath = Path.Combine(uploadsDir, fileName);
                        using var stream = new FileStream(filePath, FileMode.Create);
                        await model.ResolutionImageFile.CopyToAsync(stream);
                        request.ResolutionImagePath = $"/uploads/maintenance/{fileName}";
                    }
                }
                catch { /* non-fatal */ }
            }
        }
        else
        {
            request.ResolvedAt      = null;
            request.ResolutionNotes = null;
        }

        await _db.SaveChangesAsync();

        // Compute change flags
        var statusChanged   = oldStatus != request.Status;
        var assignedChanged = oldAssignedStaffId != request.AssignedStaffId;

        // Log the action
        var logDetails = new System.Text.StringBuilder();
        if (statusChanged)
            logDetails.Append($"Status: {oldStatus} → {request.Status}. ");
        if (assignedChanged && request.AssignedStaffId.HasValue)
            logDetails.Append($"Assigned staff ID: {request.AssignedStaffId}. ");
        if (!string.IsNullOrWhiteSpace(model.Notes))
            logDetails.Append($"Notes: {model.Notes.Trim().Substring(0, Math.Min(model.Notes.Trim().Length, 200))}.");

        _db.MaintenanceRequestLogs.Add(new MaintenanceRequestLog
        {
            RequestId         = request.RequestId,
            Action            = statusChanged ? "StatusChanged" : "Updated",
            Details           = logDetails.Length > 0 ? logDetails.ToString() : "No changes recorded.",
            PerformedByUserId = appUser.UserId,
            PerformedAt       = DateTime.Now
        });
        await _db.SaveChangesAsync();

        // Notifications
        var updaterName  = appUser.FullName;
        var notesSnippet = !string.IsNullOrWhiteSpace(model.Notes)
            ? $" — \"{model.Notes.Trim().Substring(0, Math.Min(model.Notes.Trim().Length, 80))}\""
            : "";

        if (statusChanged)
        {
            var tenantMsg = request.Status == "Resolved"
                ? $"✅ Your request \"{request.Title}\" has been RESOLVED{notesSnippet}"
                : $"🔧 Request \"{request.Title}\": {oldStatus} → {request.Status}{notesSnippet}";
            await _notifier.SendAsync(request.TenantUserId, tenantMsg, "MaintenanceUpdate");
        }

        if (appUser.Role == "MaintenanceStaff" && statusChanged)
        {
            var managers = await _db.Users.Where(u => u.Role == "PropertyManager").ToListAsync();
            foreach (var mgr in managers)
                await _notifier.SendAsync(mgr.UserId,
                    $"🔧 [{request.TicketNumber}] \"{request.Title}\": {oldStatus} → {request.Status} by {updaterName}{notesSnippet}",
                    "MaintenanceUpdate");
        }

        if (assignedChanged && request.AssignedStaffId.HasValue && appUser.Role == "PropertyManager")
            await _notifier.SendAsync(request.AssignedStaffId.Value,
                $"📋 You have been assigned to \"{request.Title}\" (Ticket: {request.TicketNumber}). Status: {request.Status}",
                "MaintenanceUpdate");

        if (statusChanged && !string.IsNullOrWhiteSpace(request.Tenant?.Email))
        {
            try
            {
                await _emailService.SendMaintenanceStatusChangedAsync(
                    toEmail:      request.Tenant.Email,
                    toName:       request.Tenant.FullName,
                    ticketNumber: request.TicketNumber ?? "",
                    title:        request.Title,
                    unitNumber:   request.Unit?.UnitNumber ?? "",
                    newStatus:    request.Status,
                    notes:        model.Notes);
            }
            catch { /* email failure must not block the flow */ }
        }

        TempData["Success"] = "Maintenance request updated successfully.";
        return RedirectToAction("Details", new { id = request.RequestId });
    }
}
