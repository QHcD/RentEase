namespace PropertyLeasing.MVC.ViewModels;

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
