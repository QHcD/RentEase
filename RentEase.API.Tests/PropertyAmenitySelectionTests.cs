using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class PropertyAmenitySelectionTests
{
    [Fact]
    public void Merge_PropertyAmenities_CombinesFixedAndCustom()
    {
        var merged = PropertyAmenitySelection.Merge(
            new[] { "Parking", "Gym" },
            new[] { "Rooftop Lounge" },
            PropertyAmenityCatalog.StandardOptions);

        Assert.Equal(new[] { "Parking", "Gym", "Rooftop Lounge" }, merged);
    }

    [Fact]
    public void JoinCustomOnly_UnitAmenities_HasNoFixedCatalog()
    {
        var joined = PropertyAmenitySelection.JoinCustomOnly(new[] { "Sea View", "Corner Unit" });
        Assert.Equal("Sea View, Corner Unit", joined);
    }

    [Fact]
    public void JoinCustomOnly_Empty_ReturnsNull()
    {
        Assert.Null(PropertyAmenitySelection.JoinCustomOnly(Array.Empty<string>()));
        Assert.Null(PropertyAmenitySelection.JoinCustomOnly(null));
    }

    [Fact]
    public void ParseCommaSeparated_SplitsStoredString()
    {
        var items = PropertyAmenitySelection.ParseCommaSeparated("Parking, Sea View , Gym");
        Assert.Equal(new[] { "Parking", "Sea View", "Gym" }, items);
    }

    [Fact]
    public void SplitFromStoredString_ReadsPropertyAmenities()
    {
        var (fixedSel, customs) = PropertyAmenitySelection.SplitFromStoredString(
            "Parking, Sea View, Rooftop Lounge",
            PropertyAmenityCatalog.StandardOptions);

        Assert.Contains("Parking", fixedSel);
        Assert.Contains("Sea View", fixedSel);
        Assert.Contains("Rooftop Lounge", customs);
    }

    [Fact]
    public void ValidateCustomAmenityList_RejectsTooMany()
    {
        var tooMany = Enumerable.Range(1, PropertyAmenitySelection.MaxCustomAmenityItems + 1)
            .Select(i => $"Amenity {i}")
            .ToList();

        Assert.NotEmpty(PropertyAmenitySelection.ValidateCustomAmenityList(tooMany));
    }

    [Fact]
    public void PropertyAndUnitAmenities_AreStoredSeparately()
    {
        var propertyJoined = PropertyAmenitySelection.JoinForUnit(
            PropertyAmenitySelection.Merge(
                new[] { "Parking", "Pool" },
                new[] { "Rooftop Lounge" },
                PropertyAmenityCatalog.StandardOptions).ToList());
        var unitJoined = PropertyAmenitySelection.JoinCustomOnly(new[] { "Sea View" });

        Assert.Contains("Parking", propertyJoined);
        Assert.Contains("Pool", propertyJoined);
        Assert.Contains("Rooftop Lounge", propertyJoined);
        Assert.DoesNotContain("Sea View", propertyJoined ?? string.Empty);
        Assert.Equal("Sea View", unitJoined);
    }

    [Fact]
    public void FindDuplicatesAgainstProperty_DetectsGymOnUnitWhenPropertyHasGym()
    {
        var property = new[] { "Parking", "Gym", "Pool" };
        var duplicates = PropertyAmenitySelection.FindDuplicatesAgainstProperty(
            new[] { "Gym", "Sea View" }, property);

        Assert.Single(duplicates);
        Assert.Equal("Gym", duplicates[0]);
    }

    [Fact]
    public void FindDuplicatesAgainstProperty_IsCaseInsensitive()
    {
        var duplicates = PropertyAmenitySelection.FindDuplicatesAgainstProperty(
            new[] { "gym" }, new[] { "Gym" });

        Assert.Single(duplicates);
        Assert.Equal("gym", duplicates[0]);
    }

    [Fact]
    public void ValidateUnitAmenitiesAgainstProperty_ReturnsErrorForDuplicate()
    {
        var errors = PropertyAmenitySelection.ValidateUnitAmenitiesAgainstProperty(
            new[] { "Gym" },
            new[] { "Parking", "Gym" });

        Assert.Single(errors);
        Assert.Contains("Gym", errors[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("property amenity", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateUnitAmenitiesAgainstProperty_AllowsUniqueUnitExtras()
    {
        Assert.Empty(PropertyAmenitySelection.ValidateUnitAmenitiesAgainstProperty(
            new[] { "Sea View", "Corner Unit" },
            new[] { "Parking", "Gym" }));
    }
}
