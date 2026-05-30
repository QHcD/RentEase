namespace PropertyLeasing.MVC.Helpers;

/// <summary>Re-exports standard amenity labels from business logic for MVC views.</summary>
public static class PropertyAmenityOptions
{
    public static IReadOnlyList<string> All => PropertyLeasing.BusinessLogic.PropertyAmenityCatalog.StandardOptions;
}
