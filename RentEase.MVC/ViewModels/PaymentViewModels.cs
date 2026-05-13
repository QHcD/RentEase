using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

public class PaymentListViewModel
{
    public int       PaymentId       { get; set; }
    public int       LeaseId         { get; set; }
    public string    UnitNumber      { get; set; } = string.Empty;
    public string    PropertyName    { get; set; } = string.Empty;
    public string    TenantName      { get; set; } = string.Empty;
    public decimal   AmountDue       { get; set; }
    public decimal?  AmountPaid      { get; set; }
    public decimal?  LateFee         { get; set; }
    public DateTime  DueDate         { get; set; }
    public DateTime? PaidDate        { get; set; }
    public string    PaymentStatus   { get; set; } = string.Empty;
    public string?   Notes           { get; set; }
    public string?   PaymentPlanType { get; set; }
    public int       InstallmentNum  { get; set; }
    public int       TotalInstallments { get; set; }
}

public class SelectPlanViewModel
{
    public int      LeaseId         { get; set; }
    public string   UnitNumber      { get; set; } = string.Empty;
    public string   PropertyName    { get; set; } = string.Empty;
    public DateTime LeaseStartDate  { get; set; }
    public DateTime LeaseEndDate    { get; set; }
    public decimal  MonthlyRent     { get; set; }
    public decimal  SecurityDeposit { get; set; }
    public int      TotalMonths     { get; set; }
    public decimal  TotalAmount     { get; set; }

    // Partial-month support
    public int      ActualDays      { get; set; }   // real calendar days
    public bool     IsPartialMonth  { get; set; }   // < 30 days → pro-rated, Full only

    [Required(ErrorMessage = "Please select a payment plan.")]
    public string? SelectedPlan    { get; set; }  // "Full" or "Monthly"
}

public class CheckoutViewModel
{
    public int     LeaseId         { get; set; }
    public string  UnitNumber      { get; set; } = string.Empty;
    public string  PropertyName    { get; set; } = string.Empty;
    public string  PlanType        { get; set; } = string.Empty;
    public decimal AmountToPay     { get; set; }
    public string  PlanDescription { get; set; } = string.Empty;
    public int     TotalMonths     { get; set; }
    public decimal MonthlyRent     { get; set; }

    [Required(ErrorMessage = "Card number is required.")]
    [StringLength(19, MinimumLength = 16, ErrorMessage = "Enter a valid card number.")]
    [Display(Name = "Card Number")]
    public string CardNumber     { get; set; } = string.Empty;

    [Required(ErrorMessage = "Expiry date is required.")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "Use MM/YY format.")]
    [Display(Name = "Expiry Date")]
    public string ExpiryDate     { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cardholder name is required.")]
    [Display(Name = "Cardholder Name")]
    public string CardholderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CVV is required.")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits.")]
    [Display(Name = "CVV")]
    public string Cvv            { get; set; } = string.Empty;
}

public class PaymentConfirmationViewModel
{
    public int      LeaseId         { get; set; }
    public string   UnitNumber      { get; set; } = string.Empty;
    public string   PropertyName    { get; set; } = string.Empty;
    public string   PlanType        { get; set; } = string.Empty;
    public decimal  TotalPaid       { get; set; }
    public DateTime PaidAt          { get; set; }
    public int      TotalInstallments { get; set; }
    public List<InstallmentSummaryViewModel> Installments { get; set; } = new();
}

public class PaymentBillViewModel
{
    // Invoice meta
    public string   InvoiceNumber    { get; set; } = string.Empty;
    public DateTime IssuedDate       { get; set; }

    // Tenant
    public string   TenantName       { get; set; } = string.Empty;
    public string?  TenantEmail      { get; set; }
    public string?  TenantPhone      { get; set; }

    // Property / unit
    public string   PropertyName     { get; set; } = string.Empty;
    public string   UnitNumber       { get; set; } = string.Empty;
    public string?  PropertyAddress  { get; set; }

    // Lease
    public DateTime LeaseStart       { get; set; }
    public DateTime LeaseEnd         { get; set; }
    public string?  PaymentPlanType  { get; set; }

    // This installment
    public int      InstallmentNum   { get; set; }
    public int      TotalInstallments{ get; set; }
    public decimal  AmountDue        { get; set; }
    public decimal  LateFee          { get; set; }
    public decimal  TotalAmount      => AmountDue + LateFee;
    public decimal? AmountPaid       { get; set; }
    public DateTime DueDate          { get; set; }
    public DateTime? PaidDate        { get; set; }
    public string   PaymentStatus    { get; set; } = string.Empty;
    public string?  Notes            { get; set; }

    // Lease-wide balance
    public decimal  LeaseTotalDue    { get; set; }
    public decimal  LeaseTotalPaid   { get; set; }
    public decimal  LeaseBalance     => LeaseTotalDue - LeaseTotalPaid;

    // All installments (for mini-ledger)
    public List<InstallmentSummaryViewModel> AllInstallments { get; set; } = new();
}

public class InstallmentSummaryViewModel
{
    public int      Number        { get; set; }
    public decimal  Amount        { get; set; }
    public DateTime DueDate       { get; set; }
    public string   Status        { get; set; } = string.Empty;
}

public class TenantPayViewModel
{
    public int      PaymentId         { get; set; }
    public int      LeaseId           { get; set; }
    public string   UnitNumber        { get; set; } = string.Empty;
    public string   PropertyName      { get; set; } = string.Empty;
    public int      InstallmentNum    { get; set; }
    public int      TotalInstallments { get; set; }
    public decimal  AmountDue         { get; set; }
    public decimal? LateFee           { get; set; }
    public decimal  TotalAmount       => AmountDue + (LateFee ?? 0);
    public DateTime DueDate           { get; set; }

    [Required(ErrorMessage = "Card number is required.")]
    [StringLength(19, MinimumLength = 16, ErrorMessage = "Enter a valid card number.")]
    [Display(Name = "Card Number")]
    public string CardNumber     { get; set; } = string.Empty;

    [Required(ErrorMessage = "Expiry date is required.")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "Use MM/YY format.")]
    [Display(Name = "Expiry Date")]
    public string ExpiryDate     { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cardholder name is required.")]
    [Display(Name = "Cardholder Name")]
    public string CardholderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CVV is required.")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits.")]
    [Display(Name = "CVV")]
    public string Cvv            { get; set; } = string.Empty;
}
