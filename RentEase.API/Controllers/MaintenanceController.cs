using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.DTOs;
using PropertyLeasing.API.Hubs;
using PropertyLeasing.API.Models;
using System.Security.Claims;

namespace PropertyLeasing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly PropertyLeasingDbContext _db;
    private readonly IHubContext<MaintenanceHub> _hub;

    public MaintenanceController(
        PropertyLeasingDbContext db,
        IHubContext<MaintenanceHub> hub)
    {
        _db  = db;
        _hub = hub;
    }

    // ──────────────────────────────────────────────────────
    // PUBLIC: No auth needed — lookup by ticket + phone
    // GET api/maintenance/lookup?ticket=TKT-2026-001&phone=+973...
    // ──────────────────────────────────────────────────────
    [HttpGet("lookup")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicLookup(
        [FromQuery] string ticket,
        [FromQuery] string phone)
    {
        if (string.IsNullOrWhiteSpace(ticket) || string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { message = "Ticket number and phone are required." });

        var request = await _db.MaintenanceRequests
            .Include(r => r.Unit).ThenInclude(u => u.Property)
            .Include(r => r.Tenant)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r =>
                r.TicketNumber == ticket &&
                r.Tenant.Phone == phone);

        if (request == null)
            return NotFound(new { message = "No request found with the provided ticket and phone." });

        var response = new MaintenanceLookupResponseDto
        {
            TicketNumber = request.TicketNumber ?? "",
            Title        = request.Title,
            Status       = request.Status,
            Priority     = request.Priority,
            RequestType  = request.RequestType ?? "",
            UnitNumber   = request.Unit.UnitNumber,
            PropertyName = request.Unit.Property.Name,
            SubmittedAt  = request.SubmittedAt,
            ResolvedAt   = request.ResolvedAt,
            History      = request.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .Select(h => new StatusHistoryDto
                {
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    Notes     = h.Notes,
                    ChangedAt = h.ChangedAt
                }).ToList()
        };

        return Ok(response);
    }

    // ──────────────────────────────────────────────────────
    // GET api/maintenance — all requests (Manager/Staff)
    // ──────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "PropertyManager,MaintenanceStaff")]
    public async Task<IActionResult> GetAll()
    {
        var requests = await _db.MaintenanceRequests
            .Include(r => r.Unit).ThenInclude(u => u.Property)
            .Include(r => r.Tenant)
            .Include(r => r.AssignedStaff)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new MaintenanceRequestDto
            {
                RequestId     = r.RequestId,
                Title         = r.Title,
                Description   = r.Description,
                RequestType   = r.RequestType,
                Priority      = r.Priority,
                Status        = r.Status,
                TicketNumber  = r.TicketNumber,
                UnitNumber    = r.Unit.UnitNumber,
                TenantName    = r.Tenant.FullName,
                AssignedStaff = r.AssignedStaff != null ? r.AssignedStaff.FullName : null,
                SubmittedAt   = r.SubmittedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    // ──────────────────────────────────────────────────────
    // POST api/maintenance — tenant submits new request
    // ──────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Create([FromBody] CreateMaintenanceRequestDto dto)
    {
        // Get tenant's internal User record via IdentityUserId
        var identityId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identityId);
        if (user == null) return Unauthorized();

        // Generate unique ticket number
        var ticketNumber = $"TKT-{DateTime.Now:yyyy}-{new Random().Next(1000, 9999)}";

        var request = new MaintenanceRequest
        {
            UnitId       = dto.UnitId,
            TenantUserId = user.UserId,
            Title        = dto.Title,
            Description  = dto.Description,
            RequestType  = dto.RequestType,
            Priority     = dto.Priority,
            Status       = "Submitted",
            TicketNumber = ticketNumber,
            SubmittedAt  = DateTime.Now
        };

        _db.MaintenanceRequests.Add(request);
        await _db.SaveChangesAsync();

        // SignalR — notify all connected staff in real time
        await _hub.Clients.Group("Staff").SendAsync("NewRequest", new
        {
            requestId    = request.RequestId,
            title        = request.Title,
            ticketNumber = request.TicketNumber,
            priority     = request.Priority,
            status       = request.Status,
            submittedAt  = request.SubmittedAt
        });

        return CreatedAtAction(nameof(GetAll), new { id = request.RequestId },
            new { message = "Request submitted.", ticketNumber });
    }

    // ──────────────────────────────────────────────────────
    // PUT api/maintenance/{id}/status — Manager updates status
    // ──────────────────────────────────────────────────────
    [HttpPut("{id}/status")]
    [Authorize(Roles = "PropertyManager,MaintenanceStaff")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
    {
        var request = await _db.MaintenanceRequests
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.RequestId == id);

        if (request == null) return NotFound();

        var identityId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var updatedBy  = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identityId);

        // Save history
        _db.MaintenanceStatusHistories.Add(new MaintenanceStatusHistory
        {
            RequestId       = request.RequestId,
            OldStatus       = request.Status,
            NewStatus       = dto.NewStatus,
            Notes           = dto.Notes,
            ChangedAt       = DateTime.Now,
            ChangedByUserId = updatedBy?.UserId
        });

        request.Status = dto.NewStatus;
        if (dto.AssignedStaffId.HasValue)
            request.AssignedStaffId = dto.AssignedStaffId;
        if (dto.NewStatus == "Resolved")
        {
            request.ResolvedAt      = DateTime.Now;
            request.ResolutionNotes = dto.Notes;
        }

        await _db.SaveChangesAsync();

        // SignalR — broadcast status update to Staff board
        await _hub.Clients.Group("Staff").SendAsync("StatusUpdated", new
        {
            requestId = request.RequestId,
            newStatus = request.Status,
            ticketNumber = request.TicketNumber
        });

        return Ok(new { message = "Status updated successfully." });
    }
}

// Small DTO just for status update
public class UpdateStatusDto
{
    public string  NewStatus      { get; set; } = string.Empty;
    public string? Notes          { get; set; }
    public int?    AssignedStaffId { get; set; }
}
