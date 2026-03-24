namespace PropertyLeasing.API.DTOs;

// ── Auth ──────────────────────────────────────────────
public class LoginDto
{
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone   { get; set; }
}

public class AuthResponseDto
{
    public string Token     { get; set; } = string.Empty;
    public string Email     { get; set; } = string.Empty;
    public string FullName  { get; set; } = string.Empty;
    public string Role      { get; set; } = string.Empty;
    public DateTime Expiry  { get; set; }
}

// ── Units ─────────────────────────────────────────────
public class UnitDto
{
    public int     UnitId             { get; set; }
    public string  UnitNumber         { get; set; } = string.Empty;
    public string? UnitType           { get; set; }
    public double? Sizesqm            { get; set; }
    public decimal? MonthlyRent       { get; set; }
    public string? Amenities          { get; set; }
    public string? AvailabilityStatus { get; set; }
    public string? PropertyName       { get; set; }
    public string? PropertyAddress    { get; set; }
}

// ── Maintenance Lookup (Public - no auth needed) ──────
public class MaintenanceLookupResponseDto
{
    public string  TicketNumber  { get; set; } = string.Empty;
    public string  Title         { get; set; } = string.Empty;
    public string  Status        { get; set; } = string.Empty;
    public string  Priority      { get; set; } = string.Empty;
    public string  RequestType   { get; set; } = string.Empty;
    public string  UnitNumber    { get; set; } = string.Empty;
    public string  PropertyName  { get; set; } = string.Empty;
    public DateTime SubmittedAt  { get; set; }
    public DateTime? ResolvedAt  { get; set; }
    public List<StatusHistoryDto> History { get; set; } = new();
}

public class StatusHistoryDto
{
    public string?  OldStatus  { get; set; }
    public string   NewStatus  { get; set; } = string.Empty;
    public string?  Notes      { get; set; }
    public DateTime ChangedAt  { get; set; }
}

// ── Maintenance Requests ──────────────────────────────
public class MaintenanceRequestDto
{
    public int     RequestId       { get; set; }
    public string  Title           { get; set; } = string.Empty;
    public string? Description     { get; set; }
    public string? RequestType     { get; set; }
    public string  Priority        { get; set; } = string.Empty;
    public string  Status          { get; set; } = string.Empty;
    public string? TicketNumber    { get; set; }
    public string  UnitNumber      { get; set; } = string.Empty;
    public string  TenantName      { get; set; } = string.Empty;
    public string? AssignedStaff   { get; set; }
    public DateTime SubmittedAt    { get; set; }
}

public class CreateMaintenanceRequestDto
{
    public int     UnitId      { get; set; }
    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RequestType { get; set; }
    public string  Priority    { get; set; } = "Medium";
}

// ── Lease Applications ────────────────────────────────
public class LeaseApplicationDto
{
    public int      ApplicationId       { get; set; }
    public string   TenantName          { get; set; } = string.Empty;
    public string   UnitNumber          { get; set; } = string.Empty;
    public string   PropertyName        { get; set; } = string.Empty;
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedEndDate   { get; set; }
    public string   Status              { get; set; } = string.Empty;
    public string?  Notes               { get; set; }
    public DateTime CreatedAt           { get; set; }
}

// ── Reports (for Reporting App) ───────────────────────
public class OccupancyReportDto
{
    public string PropertyName  { get; set; } = string.Empty;
    public int    TotalUnits    { get; set; }
    public int    OccupiedUnits { get; set; }
    public int    AvailableUnits { get; set; }
    public double OccupancyRate { get; set; }
}

public class MaintenanceReportDto
{
    public int    TotalRequests      { get; set; }
    public int    PendingRequests    { get; set; }
    public int    InProgressRequests { get; set; }
    public int    ResolvedRequests   { get; set; }
    public double AvgResolutionHours { get; set; }
}

public class PaymentReportDto
{
    public decimal TotalDue      { get; set; }
    public decimal TotalPaid     { get; set; }
    public decimal TotalOverdue  { get; set; }
    public int     OverdueCount  { get; set; }
}
