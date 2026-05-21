namespace PropertyLeasing.MVC.ViewModels;

public class PropertyListViewModel
{
    public int     PropertyId      { get; set; }
    public string  Name            { get; set; } = string.Empty;
    public string? Description     { get; set; }
    public string  Address         { get; set; } = string.Empty;
    public string? City            { get; set; }
    public string? PropertyType    { get; set; }
    public string? ImgPath         { get; set; }
    public int     TotalUnits      { get; set; }
    public int     AvailableUnits  { get; set; }
    public List<string> ImagePaths { get; set; } = new();
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
    public List<string> ImagePaths     { get; set; } = new();
}
