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

    public List<PropertyOccupancyViewModel>    PropertyOccupancy  { get; set; } = new();
    public List<MaintenanceListViewModel>      RecentMaintenance  { get; set; } = new();
    public List<LeaseApplicationListViewModel> RecentApplications { get; set; } = new();
}

public class PropertyOccupancyViewModel
{
    public string PropertyName  { get; set; } = string.Empty;
    public int    TotalUnits    { get; set; }
    public int    OccupiedUnits { get; set; }
    public double OccupancyRate { get; set; }
}
