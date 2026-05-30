using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class PropertyMonthlyRentRulesTests
{
    private static PropertyMonthlyRentRules.FloorMonthlyRentInput Floor(
        int units,
        decimal? floorRent = null,
        params decimal?[] unitRents) =>
        new(units, floorRent, unitRents);

    [Fact]
    public void NormalizeOptionalRent_RequiresAtLeastTenBd()
    {
        Assert.Null(PropertyMonthlyRentRules.NormalizeOptionalRent(null));
        Assert.Null(PropertyMonthlyRentRules.NormalizeOptionalRent(0m));
        Assert.Null(PropertyMonthlyRentRules.NormalizeOptionalRent(9.999m));
        Assert.Equal(10m, PropertyMonthlyRentRules.NormalizeOptionalRent(10m));
        Assert.Equal(10.001m, PropertyMonthlyRentRules.NormalizeOptionalRent(10.001m));
    }

    [Fact]
    public void ValidateMonthlyRentLayout_RejectsRentBelowMinimum()
    {
        var errors = PropertyMonthlyRentRules.ValidateMonthlyRentLayout(5m, new[] { Floor(1) });
        Assert.Contains(errors, e => e.Contains("Minimum monthly rent is BD 10.000", StringComparison.OrdinalIgnoreCase));

        errors = PropertyMonthlyRentRules.ValidateMonthlyRentLayout(null, new[]
        {
            Floor(1, floorRent: 9m)
        });
        Assert.Contains(errors, e => e.Contains("Minimum monthly rent is BD 10.000", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveUnitMonthlyRent_UsesUnitThenFloorThenProperty()
    {
        Assert.Equal(500m, PropertyMonthlyRentRules.ResolveUnitMonthlyRent(100m, 200m, 500m));
        Assert.Equal(200m, PropertyMonthlyRentRules.ResolveUnitMonthlyRent(100m, 200m, null));
        Assert.Equal(100m, PropertyMonthlyRentRules.ResolveUnitMonthlyRent(100m, null, null));
        Assert.Null(PropertyMonthlyRentRules.ResolveUnitMonthlyRent(null, null, null));
    }

    [Fact]
    public void ResolveAllUnitRents_PropertyDefault_AppliesToAllUnits()
    {
        var rents = PropertyMonthlyRentRules.ResolveAllUnitRents(350m, new[]
        {
            Floor(2),
            Floor(3)
        });

        Assert.Equal(new[] { 350m, 350m, 350m, 350m, 350m }, rents);
    }

    [Fact]
    public void ResolveAllUnitRents_FloorDefault_AppliesToUnitsOnThatFloor()
    {
        var rents = PropertyMonthlyRentRules.ResolveAllUnitRents(null, new[]
        {
            Floor(3, floorRent: 400m),
            Floor(2, floorRent: 250m)
        });

        Assert.Equal(new[] { 400m, 400m, 400m, 250m, 250m }, rents);
    }

    [Fact]
    public void ResolveAllUnitRents_UnitOverridesFloorAndProperty()
    {
        var rents = PropertyMonthlyRentRules.ResolveAllUnitRents(100m, new[]
        {
            Floor(2, floorRent: 200m, 500m, null)
        });

        Assert.Equal(new[] { 500m, 200m }, rents);
    }

    [Fact]
    public void ResolveAllUnitRents_MixedFloors_UnitAndFloorSources()
    {
        var rents = PropertyMonthlyRentRules.ResolveAllUnitRents(null, new[]
        {
            Floor(3, floorRent: 300m),
            Floor(3, floorRent: null, 410m, 420m, 430m)
        });

        Assert.Equal(new[] { 300m, 300m, 300m, 410m, 420m, 430m }, rents);
    }

    [Fact]
    public void ValidateMonthlyRentLayout_RejectsWhenAllBoxesEmpty()
    {
        var errors = PropertyMonthlyRentRules.ValidateMonthlyRentLayout(null, new[]
        {
            Floor(2),
            Floor(1)
        });

        Assert.Contains(errors, e => e.Contains("property, floor, or unit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateMonthlyRentLayout_RejectsUnresolvedUnit()
    {
        var errors = PropertyMonthlyRentRules.ValidateMonthlyRentLayout(null, new[]
        {
            Floor(2, floorRent: 300m),
            Floor(1)
        });

        Assert.Contains(errors, e => e.Contains("Floor 2, unit 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateMonthlyRentLayout_AcceptsValidMixedLayout()
    {
        var floors = new[]
        {
            Floor(3, floorRent: 300m),
            Floor(3, floorRent: null, 410m, 420m, 430m)
        };

        Assert.Empty(PropertyMonthlyRentRules.ValidateMonthlyRentLayout(null, floors));
        Assert.Equal(6, PropertyMonthlyRentRules.ResolveAllUnitRents(null, floors).Count);
    }
}
