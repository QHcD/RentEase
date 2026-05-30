using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class PropertyAreaAllocationRulesTests
{
    [Fact]
    public void CommonArea_IsSevenPercentOfFloorPlate()
    {
        var common = PropertyAreaAllocationRules.GetCommonAreaSqm(1000m);
        Assert.Equal(70m, common);
        Assert.Equal(930m, PropertyAreaAllocationRules.GetRentableAreaSqm(1000m));
    }

    [Fact]
    public void SplitPropertyAcrossFloors_RepeatsSamePlateForEveryFloor()
    {
        var parts = PropertyAreaAllocationRules.SplitPropertyAcrossFloors(1000m, 3);
        Assert.Equal(3, parts.Count);
        Assert.All(parts, p => Assert.Equal(1000m, p));
    }

    [Fact]
    public void BuildDefaultFloorPlans_FloorCountDoesNotShrinkPerFloorPlate()
    {
        var oneFloor = PropertyAreaAllocationRules.BuildDefaultFloorPlans(500m, new[] { 1 });
        var threeFloors = PropertyAreaAllocationRules.BuildDefaultFloorPlans(500m, new[] { 1, 1, 1 });

        Assert.Equal(500m, oneFloor[0].FloorPlateSqm);
        Assert.All(threeFloors, p => Assert.Equal(500m, p.FloorPlateSqm));
    }

    [Fact]
    public void BuildDefaultUnitAreas_SumsToRentable()
    {
        const decimal floorPlate = 500m;
        var rentable = PropertyAreaAllocationRules.GetRentableAreaSqm(floorPlate);
        var units = PropertyAreaAllocationRules.BuildDefaultUnitAreas(floorPlate, 4);
        Assert.Equal(4, units.Count);
        Assert.True(PropertyAreaAllocationRules.NearlyEqual(units.Sum(), rentable));
    }

    [Fact]
    public void BuildDefaultUnitAreas_DividesRentableSpaceAcrossSevenUnits()
    {
        const decimal floorPlate = 500m;
        var rentable = PropertyAreaAllocationRules.GetRentableAreaSqm(floorPlate);
        Assert.Equal(465m, rentable);
        Assert.Equal(66.43m, PropertyAreaAllocationRules.GetDefaultUnitAreaSqm(floorPlate, 7));

        var units = PropertyAreaAllocationRules.BuildDefaultUnitAreas(floorPlate, 7);
        Assert.Equal(7, units.Count);
        Assert.True(PropertyAreaAllocationRules.NearlyEqual(units.Sum(), rentable));
        Assert.All(units, u => Assert.True(u >= PropertyAreaAllocationRules.MinUnitAreaSqm));
    }

    [Fact]
    public void ValidatePropertyAreaAllocationDetailed_AcceptsBalancedDefaults()
    {
        var plans = PropertyAreaAllocationRules.BuildDefaultFloorPlans(2000m, new[] { 2, 3 });
        var floors = plans
            .Select(p => (p.UnitAreasSqm.Count, (IReadOnlyList<decimal>)p.UnitAreasSqm))
            .ToList();

        var (errors, warnings) = PropertyAreaAllocationRules.ValidatePropertyAreaAllocationDetailed(2000m, floors);
        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidatePropertyAreaAllocationDetailed_RejectsOverRentable()
    {
        const decimal floorPlate = 500m;
        var rentable = PropertyAreaAllocationRules.GetRentableAreaSqm(floorPlate);
        var floors = new List<(int, IReadOnlyList<decimal>)>
        {
            (4, new List<decimal> { 20m, 50m, 20m, rentable - 80m + 1m })
        };

        var (errors, warnings) = PropertyAreaAllocationRules.ValidatePropertyAreaAllocationDetailed(floorPlate, floors);
        Assert.Contains(errors, e => e.Contains("exceeds rentable"));
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidatePropertyAreaAllocationDetailed_WarnsOnUnderRentable()
    {
        const decimal floorPlate = 500m;
        var floors = new List<(int, IReadOnlyList<decimal>)>
        {
            (4, new List<decimal> { 20m, 50m, 20m, 10m })
        };

        var (errors, warnings) = PropertyAreaAllocationRules.ValidatePropertyAreaAllocationDetailed(floorPlate, floors);
        Assert.Empty(errors);
        Assert.Contains(warnings, w => w.Contains("not assigned"));
    }

    [Fact]
    public void ValidatePropertySizeForLayout_RejectsTooSmallProperty()
    {
        var errors = PropertyAreaAllocationRules.ValidatePropertySize(500m, 1, new[] { 50 });
        Assert.Contains(errors, e => e.Contains("too small"));
    }

    [Fact]
    public void ValidateUnitsOnFloor_RejectsTooManyUnitsForRentableArea()
    {
        const decimal floorPlate = 500m;
        var maxUnits = PropertyAreaAllocationRules.GetMaxUnitsOnFloor(floorPlate);

        Assert.Empty(PropertyAreaAllocationRules.ValidateUnitsOnFloor(floorPlate, maxUnits, 1));

        var errors = PropertyAreaAllocationRules.ValidateUnitsOnFloor(floorPlate, maxUnits + 1, 1);
        Assert.Contains(errors, e => e.Contains("at most"));
    }

    [Fact]
    public void ValidatePropertyAreaAllocation_RejectsTooSmallUnit()
    {
        var floorPlate = 500m;
        var rentable = PropertyAreaAllocationRules.GetRentableAreaSqm(floorPlate);
        var floors = new List<(int, IReadOnlyList<decimal>)>
        {
            (1, new List<decimal> { rentable })
        };

        var errors = PropertyAreaAllocationRules.ValidatePropertyAreaAllocation(floorPlate, floors);
        Assert.Empty(errors);

        floors = new List<(int, IReadOnlyList<decimal>)>
        {
            (1, new List<decimal> { 5m })
        };
        errors = PropertyAreaAllocationRules.ValidatePropertyAreaAllocation(floorPlate, floors);
        Assert.Contains(errors, e => e.Contains("at least"));
    }

    [Fact]
    public void FlattenUnitAreasSqm_MatchesUnitNamingOrder()
    {
        var plans = PropertyAreaAllocationRules.BuildDefaultFloorPlans(1500m, new[] { 2, 1 });
        var floors = plans
            .Select(p => (p.UnitAreasSqm.Count, (IReadOnlyList<decimal>)p.UnitAreasSqm))
            .ToList();

        var flat = PropertyAreaAllocationRules.FlattenUnitAreasSqm(1500m, floors);
        var numbers = PropertyCreateUnitNaming.BuildUnitNumbers(new[]
        {
            ((string?)null, 2),
            ((string?)null, 1)
        });

        Assert.Equal(numbers.Count, flat.Count);
        Assert.Equal(3, flat.Count);
    }

    [Fact]
    public void BuildDefaultFloorPlans_TwoFloors_EachFloorUsesItsOwnRentableShare()
    {
        var plans = PropertyAreaAllocationRules.BuildDefaultFloorPlans(500m, new[] { 4, 1 });
        Assert.Equal(2, plans.Count);

        foreach (var plan in plans)
        {
            var rentable = PropertyAreaAllocationRules.GetRentableAreaSqm(plan.FloorPlateSqm);
            Assert.True(PropertyAreaAllocationRules.NearlyEqual(plan.UnitAreasSqm.Sum(), rentable));
        }
    }

    [Theory]
    [InlineData(49)]
    [InlineData(1_000_001)]
    public void ValidatePropertySize_RejectsOutOfRange(decimal size)
    {
        Assert.NotEmpty(PropertyAreaAllocationRules.ValidatePropertySize(size));
    }
}
