namespace PropertyLeasing.MVC.ViewModels;

public class DashboardViewModel
{
    public int TotalProperties          { get; set; }
    public int TotalUnits               { get; set; }
    public int AvailableUnits           { get; set; }
    public int OccupiedUnits            { get; set; }
    public int PendingApplications      { get; set; }
    public int ActiveLeases             { get; set; }
    public int OpenMaintenanceRequests  { get; set; }
    public int OverduePayments          { get; set; }

    // Refund stats
    public int     TotalRefunds          { get; set; }
    public decimal TotalRefundAmount     { get; set; }
    public decimal ThisMonthRefundAmount { get; set; }

    public List<PropertyOccupancyViewModel>    PropertyOccupancy  { get; set; } = new();
    public List<MaintenanceListViewModel>      RecentMaintenance  { get; set; } = new();
    public List<LeaseApplicationListViewModel> RecentApplications { get; set; } = new();
    public List<RefundSummaryViewModel>        RecentRefunds      { get; set; } = new();
}

public class RefundSummaryViewModel
{
    public int      RefundId       { get; set; }
    public string   TenantName     { get; set; } = string.Empty;
    public string   UnitNumber     { get; set; } = string.Empty;
    public string   PropertyName   { get; set; } = string.Empty;
    public DateTime CancelledAt    { get; set; }
    public int      MonthsConsumed { get; set; }
    public int      MonthsRefunded { get; set; }
    public decimal  TotalPaid      { get; set; }
    public decimal  RefundAmount   { get; set; }
}

public class PropertyOccupancyViewModel
{
    public string PropertyName  { get; set; } = string.Empty;
    public int    TotalUnits    { get; set; }
    public int    OccupiedUnits { get; set; }
    public double OccupancyRate { get; set; }
}
