using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

public class CreateMaintenanceViewModel
{
    public int UnitId { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Request Type")]
    [Required(ErrorMessage = "Please select a category.")]
    [RegularExpression("^(Plumbing|Electrical|HVAC|Carpentry|General)$",
        ErrorMessage = "Category must be Plumbing, Electrical, HVAC, Carpentry, or General.")]
    public string? RequestType { get; set; }

    public string Priority { get; set; } = "Medium";

    public string UnitNumber   { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;

    // Populated when tenant has multiple active leases
    public List<UnitSelectOption> AvailableUnits { get; set; } = new();

    // Optional photo upload
    [Display(Name = "Attach Photo")]
    public IFormFile? ImageFile { get; set; }
}

public class UnitSelectOption
{
    public int    UnitId       { get; set; }
    public string UnitNumber   { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string Display      => $"Unit {UnitNumber} — {PropertyName}";
}

public class MaintenanceListViewModel
{
    public int      RequestId     { get; set; }
    public string   Title         { get; set; } = string.Empty;
    public string?  Description   { get; set; }
    public string?  RequestType   { get; set; }
    public string   Priority      { get; set; } = string.Empty;
    public string   Status        { get; set; } = string.Empty;
    public string?  TicketNumber  { get; set; }
    public string   UnitNumber    { get; set; } = string.Empty;
    public string   PropertyName  { get; set; } = string.Empty;
    public string   TenantName    { get; set; } = string.Empty;
    public string?  AssignedStaff { get; set; }
    public DateTime SubmittedAt        { get; set; }
    public DateTime? ResolvedAt        { get; set; }
    public string?  CancellationReason { get; set; }
}

public class UpdateMaintenanceViewModel
{
    public int     RequestId       { get; set; }
    public string  Title           { get; set; } = string.Empty;
    public string? Description     { get; set; }
    public string? RequestType     { get; set; }
    public string? ImagePath       { get; set; }   // tenant complaint photo
    public string  CurrentStatus   { get; set; } = string.Empty;
    public string  NewStatus       { get; set; } = string.Empty;
    public string? Notes           { get; set; }
    public int?    AssignedStaffId { get; set; }
    public List<StaffSelectItem> StaffList { get; set; } = new();

    // Resolution proof photo (uploaded when resolving)
    [Display(Name = "Resolution Proof Photo")]
    public IFormFile? ResolutionImageFile { get; set; }
}

public class StaffSelectItem
{
    public int     UserId            { get; set; }
    public string  FullName          { get; set; } = string.Empty;
    public string? SkillProfile      { get; set; }
    public string? AvailabilityStatus { get; set; }
}

public class PublicLookupViewModel
{
    [Required]
    [Display(Name = "Ticket Number")]
    public string? TicketNumber { get; set; }

    [Required]
    [Phone]
    [Display(Name = "Registered Phone Number")]
    public string? Phone { get; set; }

    public MaintenanceLookupResultViewModel? Result { get; set; }
    public bool    Searched     { get; set; }
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
    public string?  OldStatus       { get; set; }
    public string   NewStatus       { get; set; } = string.Empty;
    public string?  Notes           { get; set; }
    public DateTime ChangedAt       { get; set; }
    public string   ChangedByName   { get; set; } = string.Empty;
}

public class MaintenanceDetailViewModel
{
    public int       RequestId       { get; set; }
    public string    TicketNumber    { get; set; } = string.Empty;
    public string    Title           { get; set; } = string.Empty;
    public string?   Description     { get; set; }
    public string?   RequestType     { get; set; }
    public string    Priority        { get; set; } = string.Empty;
    public string    Status          { get; set; } = string.Empty;
    public string    UnitNumber      { get; set; } = string.Empty;
    public string    PropertyName    { get; set; } = string.Empty;
    public string    TenantName      { get; set; } = string.Empty;
    public string?   TenantEmail     { get; set; }
    public string?   AssignedStaff   { get; set; }
    public int?      AssignedStaffId { get; set; }
    public DateTime  SubmittedAt     { get; set; }
    public DateTime? ResolvedAt      { get; set; }
    public DateTime? ScheduledDate   { get; set; }
    public string?   ResolutionNotes      { get; set; }
    public string?   ImagePath            { get; set; }
    public string?   ResolutionImagePath  { get; set; }
    public string?   CancellationReason   { get; set; }
    public List<StatusHistoryViewModel> History   { get; set; } = new();
    public List<StaffSelectItem>        StaffList { get; set; } = new();
}

public class ExpiringLeaseViewModel
{
    public int      LeaseId        { get; set; }
    public int      UnitId         { get; set; }
    public string   UnitNumber     { get; set; } = string.Empty;
    public string   PropertyName   { get; set; } = string.Empty;
    public string   TenantName     { get; set; } = string.Empty;
    public int      TenantUserId   { get; set; }
    public DateTime LeaseEndDate   { get; set; }
    public int      DaysRemaining  { get; set; }
    public bool     HasOpenRequest { get; set; }
}
