using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.Reporting.ViewModels;

// ── Auth ──────────────────────────────────────────────
public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

// ── API Response Models (mirrors API DTOs) ────────────
public class AuthResponse
{
    public string Token    { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role     { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}

public class OccupancyReportItem
{
    public string PropertyName   { get; set; } = string.Empty;
    public int    TotalUnits     { get; set; }
    public int    OccupiedUnits  { get; set; }
    public int    AvailableUnits { get; set; }
    public double OccupancyRate  { get; set; }
}

public class MaintenanceReportItem
{
    public int    TotalRequests      { get; set; }
    public int    PendingRequests    { get; set; }
    public int    InProgressRequests { get; set; }
    public int    ResolvedRequests   { get; set; }
    public double AvgResolutionHours { get; set; }
}

public class PaymentReportItem
{
    public decimal TotalDue     { get; set; }
    public decimal TotalPaid    { get; set; }
    public decimal TotalOverdue { get; set; }
    public int     OverdueCount { get; set; }
}

public class ApplicationReportItem
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

// ── Combined Dashboard ────────────────────────────────
public class ReportDashboardViewModel
{
    public List<OccupancyReportItem>  OccupancyReport    { get; set; } = new();
    public MaintenanceReportItem?     MaintenanceReport  { get; set; }
    public PaymentReportItem?         PaymentReport      { get; set; }
    public List<ApplicationReportItem> Applications      { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
