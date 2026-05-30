namespace PropertyLeasing.BusinessLogic;

/// <summary>Standard property amenity labels selectable when creating or editing a property.</summary>
public static class PropertyAmenityCatalog
{
    public static readonly IReadOnlyList<string> StandardOptions = new[]
    {
        "Parking",
        "Gym",
        "Pool",
        "Sea View",
        "Balcony",
        "Central AC",
        "24h Security",
        "Elevator",
        "Pet Friendly",
        "Concierge"
    };
}
