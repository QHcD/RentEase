using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

public class CreateLeaseApplicationViewModel
{
    public int      UnitId       { get; set; }
    public string   UnitNumber   { get; set; } = string.Empty;
    public string   PropertyName { get; set; } = string.Empty;
    public decimal? MonthlyRent  { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Preferred Start Date")]
    public DateTime RequestedStartDate { get; set; } = DateTime.Today.AddDays(2);

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Preferred End Date")]
    public DateTime RequestedEndDate { get; set; } = DateTime.Today.AddDays(2).AddMonths(6);

    [StringLength(500)]
    public string? Notes { get; set; }

    // Helpers for HTML min/max attributes
    public string MinStartDate => DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");
    public string MaxStartDate => DateTime.Today.AddMonths(2).ToString("yyyy-MM-dd");
}

// ── Renew Lease Application form ─────────────────────────────────────────────
public class ApplyRenewViewModel
{
    public int      LeaseId          { get; set; }
    public string   UnitNumber       { get; set; } = string.Empty;
    public string   PropertyName     { get; set; } = string.Empty;
    public decimal? MonthlyRent      { get; set; }

    // Fixed start date: the day after the parent lease ends
    public DateTime RequestedStartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Requested End Date")]
    public DateTime RequestedEndDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public string MaxEndDate => RequestedStartDate.AddYears(1).ToString("yyyy-MM-dd");
    public string MinEndDate => RequestedStartDate.AddDays(1).ToString("yyyy-MM-dd");
}

// ── Terminate Active Lease form ───────────────────────────────────────────────
public class TerminateLeaseViewModel
{
    public int     LeaseId      { get; set; }
    public string  UnitNumber   { get; set; } = string.Empty;
    public string  PropertyName { get; set; } = string.Empty;
    public DateTime LeaseEndDate { get; set; }
    public int?    TerminationId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Termination Date")]
    public DateTime TerminationDate { get; set; } = DateTime.Today.AddDays(2);

    [StringLength(500)]
    public string? Notes { get; set; }

    public string MinTerminationDate => DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");
    public string MaxTerminationDate => LeaseEndDate.ToString("yyyy-MM-dd");
}

// ── Flat list item (tenant view & dashboard snippets) ─────────────────────────
public class LeaseApplicationListViewModel
{
    public int       ApplicationId      { get; set; }
    public string    UnitNumber         { get; set; } = string.Empty;
    public string    PropertyName       { get; set; } = string.Empty;
    public string    TenantName         { get; set; } = string.Empty;
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedEndDate   { get; set; }
    public string    Status             { get; set; } = string.Empty;
    public string?   Notes              { get; set; }
    public DateTime  CreatedAt          { get; set; }
    public int?      ParentLeaseId      { get; set; }
    public bool      IsRenewal          => ParentLeaseId.HasValue;
    public bool      CanCancel          => Status != "Rejected" && Status != "Canceled";
}

// ── Manager's grouped-by-unit view ────────────────────────────────────────────
public class UnitApplicationGroupViewModel
{
    public int     UnitId             { get; set; }
    public string  UnitNumber         { get; set; } = string.Empty;
    public string  PropertyName       { get; set; } = string.Empty;
    public string? AvailabilityStatus { get; set; }
    public int     ApplicationCount   { get; set; }
    public List<LeaseApplicationListViewModel> Applications { get; set; } = new();
}

// ── Full detail (Details page) ────────────────────────────────────────────────
public class LeaseApplicationDetailViewModel
{
    public int       ApplicationId      { get; set; }
    public string    UnitNumber         { get; set; } = string.Empty;
    public string    PropertyName       { get; set; } = string.Empty;
    public string    TenantName         { get; set; } = string.Empty;
    public string?   TenantPhone        { get; set; }
    public string?   TenantEmail        { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedEndDate   { get; set; }
    public string    Status             { get; set; } = string.Empty;
    public string?   Notes              { get; set; }
    public DateTime  CreatedAt          { get; set; }
    public int?      ParentLeaseId      { get; set; }
    public bool      IsRenewal          => ParentLeaseId.HasValue;
    public bool      CanCancel          => Status != "Rejected" && Status != "Canceled";
    public List<LeaseApplicationLogViewModel> Logs { get; set; } = new();
}

// ── Log entry (read-only, inside Details) ─────────────────────────────────────
public class LeaseApplicationLogViewModel
{
    public string   Status            { get; set; } = string.Empty;
    public string   ChangedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt         { get; set; }
}

// ── Lease flat list item ──────────────────────────────────────────────────────
public class LeaseListViewModel
{
    public int      LeaseId                  { get; set; }
    public int      ApplicationId            { get; set; }
    public string   UnitNumber               { get; set; } = string.Empty;
    public string   PropertyName             { get; set; } = string.Empty;
    public string   TenantName               { get; set; } = string.Empty;
    public DateTime LeaseStartDate           { get; set; }
    public DateTime LeaseEndDate             { get; set; }
    public decimal  MonthlyRent              { get; set; }
    public decimal  SecurityDeposit          { get; set; }
    // PendingPayment / Approved / Active / Terminated / Renewed
    public string   Status                   { get; set; } = string.Empty;
    public string?  PaymentPlanType          { get; set; }
    public DateTime CreatedAt                { get; set; }
    public int      GracePeriodDays          { get; set; }
    public decimal  LateFeePercent           { get; set; }

    // Renewal & termination FKs (for action-button logic in LeaseDetails view)
    public int?     TerminationId            { get; set; }
    public DateTime? TerminationDate         { get; set; }
    public string?  TerminationNotes         { get; set; }
    public int?     RenewLeaseApplicationId  { get; set; }
    public string?  RenewApplicationStatus   { get; set; }

    // Tenant contact (populated for manager view)
    public string?  TenantEmail  { get; set; }
    public string?  TenantPhone  { get; set; }

    public List<LeaseLogViewModel>      Logs     { get; set; } = new();
    public List<PaymentSummaryViewModel> Payments { get; set; } = new();
}

// ── Payment summary row (shown inside Lease Details) ─────────────────────────
public class PaymentSummaryViewModel
{
    public int       PaymentId     { get; set; }
    public int       InstallmentNum { get; set; }
    public int       TotalInstallments { get; set; }
    public decimal   AmountDue     { get; set; }
    public decimal?  AmountPaid    { get; set; }
    public decimal?  LateFee       { get; set; }
    public DateTime  DueDate       { get; set; }
    public DateTime? PaidDate      { get; set; }
    public string    Status        { get; set; } = string.Empty;
    public string?   Notes         { get; set; }
}

// ── Lease log entry (read-only) ───────────────────────────────────────────────
public class LeaseLogViewModel
{
    public string   Status            { get; set; } = string.Empty;
    public string   ChangedByUserName { get; set; } = string.Empty;
    public string?  Notes             { get; set; }
    public DateTime CreatedAt         { get; set; }
}

// ── Unified Applications & Leases page ───────────────────────────────────────
public class ApplicationsAndLeasesViewModel
{
    public bool   IsManager { get; set; }

    // ── Active tab: "applications" or "leases" ────────────────────────────
    public string ActiveTab { get; set; } = "applications";

    // ── Applications tab ─────────────────────────────────────────────────
    public string AppStatusFilter { get; set; } = "All";

    // All statuses shown as sub-tabs (including new Canceled)
    public static readonly string[] AppStatuses   = { "All", "Pending", "Screening", "Approved", "Rejected", "Canceled" };
    // All lease statuses (Approved replaces Expired)
    public static readonly string[] LeaseStatuses = { "All", "PendingPayment", "Approved", "Active", "Terminated", "Renewed" };

    /// Tenant sees a flat list; manager sees this too (flat, sorted by unit)
    public List<LeaseApplicationListViewModel> Applications { get; set; } = new();

    /// Manager-only: applications grouped by unit for the Applications tab
    public List<UnitApplicationGroupViewModel> ApplicationGroups { get; set; } = new();

    /// Count of applications per status (used for badge counts on sub-tabs)
    public Dictionary<string, int> AppCounts { get; set; } = new();

    // ── Leases tab ────────────────────────────────────────────────────────
    public string LeaseStatusFilter { get; set; } = "All";
    public List<LeaseListViewModel> Leases { get; set; } = new();

    /// Count of leases per status (used for badge counts on sub-tabs)
    public Dictionary<string, int> LeaseCounts { get; set; } = new();
}
