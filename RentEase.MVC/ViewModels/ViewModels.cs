using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

// ── Auth ──────────────────────────────────────────────
public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }
}

public class ProfileViewModel
{
    public string FullName   { get; set; } = string.Empty;
    public string Email      { get; set; } = string.Empty;
    public string? Phone     { get; set; }
    public string Role       { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string? ConfirmNewPassword { get; set; }
}

// ── Properties / Units ───────────────────────────────
public class PropertyListViewModel
{
    public int     PropertyId   { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public string  Address      { get; set; } = string.Empty;
    public string? City         { get; set; }
    public string? PropertyType { get; set; }
    public string? ImgPath      { get; set; }
    public int     TotalUnits   { get; set; }
    public int     AvailableUnits { get; set; }
}

public class UnitListViewModel
{
    public int      UnitId             { get; set; }
    public string   UnitNumber         { get; set; } = string.Empty;
    public string?  UnitType           { get; set; }
    public double?  Sizesqm            { get; set; }
    public decimal? MonthlyRent        { get; set; }
    public string?  Amenities          { get; set; }
    public string?  AvailabilityStatus { get; set; }
    public string?  ImgPath            { get; set; }
    public string   PropertyName       { get; set; } = string.Empty;
    public string   PropertyAddress    { get; set; } = string.Empty;
    public int      PropertyId         { get; set; }
    public double   AverageRating      { get; set; }
    public int      FeedbackCount      { get; set; }
}

// ── Lease Application ────────────────────────────────
public class CreateLeaseApplicationViewModel
{
    public int UnitId { get; set; }
    public string UnitNumber    { get; set; } = string.Empty;
    public string PropertyName  { get; set; } = string.Empty;
    public decimal? MonthlyRent { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Preferred Start Date")]
    public DateTime RequestedStartDate { get; set; } = DateTime.Now.AddDays(7);

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Preferred End Date")]
    public DateTime RequestedEndDate { get; set; } = DateTime.Now.AddDays(7).AddYears(1);

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class LeaseApplicationListViewModel
{
    public int      ApplicationId       { get; set; }
    public string   UnitNumber          { get; set; } = string.Empty;
    public string   PropertyName        { get; set; } = string.Empty;
    public string   TenantName          { get; set; } = string.Empty;
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedEndDate   { get; set; }
    public string   Status              { get; set; } = string.Empty;
    public string?  Notes               { get; set; }
    public DateTime CreatedAt           { get; set; }
}

// ── Maintenance ──────────────────────────────────────
public class CreateMaintenanceViewModel
{
    public int UnitId { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Request Type")]
    public string? RequestType { get; set; }

    public string Priority { get; set; } = "Medium";

    public string UnitNumber   { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
}

public class MaintenanceListViewModel
{
    public int      RequestId      { get; set; }
    public string   Title          { get; set; } = string.Empty;
    public string?  Description    { get; set; }
    public string?  RequestType    { get; set; }
    public string   Priority       { get; set; } = string.Empty;
    public string   Status         { get; set; } = string.Empty;
    public string?  TicketNumber   { get; set; }
    public string   UnitNumber     { get; set; } = string.Empty;
    public string   PropertyName   { get; set; } = string.Empty;
    public string   TenantName     { get; set; } = string.Empty;
    public string?  AssignedStaff  { get; set; }
    public DateTime SubmittedAt    { get; set; }
    public DateTime? ResolvedAt    { get; set; }
}

public class UpdateMaintenanceViewModel
{
    public int     RequestId      { get; set; }
    public string  Title          { get; set; } = string.Empty;
    public string  CurrentStatus  { get; set; } = string.Empty;
    public string  NewStatus      { get; set; } = string.Empty;
    public string? Notes          { get; set; }
    public int?    AssignedStaffId { get; set; }
    public List<StaffSelectItem> StaffList { get; set; } = new();
}

public class StaffSelectItem
{
    public int    UserId   { get; set; }
    public string FullName { get; set; } = string.Empty;
}

// ── Public Lookup ────────────────────────────────────
public class PublicLookupViewModel
{
    [Required]
    [Display(Name = "Ticket Number")]
    public string? TicketNumber { get; set; }

    [Required]
    [Phone]
    [Display(Name = "Registered Phone Number")]
    public string? Phone { get; set; }

    // Result
    public MaintenanceLookupResultViewModel? Result { get; set; }
    public bool Searched { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MaintenanceLookupResultViewModel
{
    public string   TicketNumber { get; set; } = string.Empty;
    public string   Title        { get; set; } = string.Empty;
    public string   Status       { get; set; } = string.Empty;
    public string   Priority     { get; set; } = string.Empty;
    public string   RequestType  { get; set; } = string.Empty;
    public string   UnitNumber   { get; set; } = string.Empty;
    public string   PropertyName { get; set; } = string.Empty;
    public DateTime SubmittedAt  { get; set; }
    public DateTime? ResolvedAt  { get; set; }
    public List<StatusHistoryViewModel> History { get; set; } = new();
}

public class StatusHistoryViewModel
{
    public string?  OldStatus { get; set; }
    public string   NewStatus { get; set; } = string.Empty;
    public string?  Notes     { get; set; }
    public DateTime ChangedAt { get; set; }
}

// ── Notifications ────────────────────────────────────
public class NotificationViewModel
{
    public int      NotificationId   { get; set; }
    public string   Message          { get; set; } = string.Empty;
    public string?  NotificationType { get; set; }
    public string   Status           { get; set; } = string.Empty;
    public DateTime CreatedAt        { get; set; }
    public bool     IsUnread => Status == "Unread";
}

// ── Feedback ─────────────────────────────────────────
public class CreateFeedbackViewModel
{
    public int UnitId { get; set; }
    public string UnitNumber   { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int? Rating { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}

// ── Dashboard ────────────────────────────────────────
public class DashboardViewModel
{
    public int TotalProperties     { get; set; }
    public int TotalUnits          { get; set; }
    public int AvailableUnits      { get; set; }
    public int OccupiedUnits       { get; set; }
    public int PendingApplications { get; set; }
    public int ActiveLeases        { get; set; }
    public int OpenMaintenanceRequests { get; set; }
    public int OverduePayments     { get; set; }
    public List<PropertyOccupancyViewModel> PropertyOccupancy { get; set; } = new();
    public List<MaintenanceListViewModel> RecentMaintenance   { get; set; } = new();
    public List<LeaseApplicationListViewModel> RecentApplications { get; set; } = new();
}

public class PropertyOccupancyViewModel
{
    public string PropertyName   { get; set; } = string.Empty;
    public int    TotalUnits     { get; set; }
    public int    OccupiedUnits  { get; set; }
    public double OccupancyRate  { get; set; }
}

// ── Payments ─────────────────────────────────────────
public class PaymentListViewModel
{
    public int      PaymentId     { get; set; }
    public string   UnitNumber    { get; set; } = string.Empty;
    public string   PropertyName  { get; set; } = string.Empty;
    public string   TenantName    { get; set; } = string.Empty;
    public decimal  AmountDue     { get; set; }
    public decimal? AmountPaid    { get; set; }
    public DateTime DueDate       { get; set; }
    public DateTime? PaidDate     { get; set; }
    public string   PaymentStatus { get; set; } = string.Empty;
    public string?  Notes         { get; set; }
}
