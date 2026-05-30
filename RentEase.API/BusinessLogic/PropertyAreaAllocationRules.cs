namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Splits rentable area across units on a floor, reserving a fixed share for circulation (paths, elevators).
/// The entered property size is the gross plate area for each floor (all floors are the same size).
/// </summary>
public static class PropertyAreaAllocationRules
{
    /// <summary>Industry-typical gross-to-net loss for residential/commercial cores and circulation.</summary>
    public const decimal CommonAreaFraction = 0.07m;

    public const decimal MinPropertySizeSqm = 50m;
    public const decimal MaxPropertySizeSqm = 1_000_000m;
    public const decimal MinUnitAreaSqm     = 10m;
    public const decimal MaxUnitAreaSqm     = 50_000m;
    public const decimal AreaToleranceSqm   = 0.05m;

    public sealed record FloorAreaPlan(
        int FloorNumber,
        decimal FloorPlateSqm,
        decimal CommonAreaSqm,
        IReadOnlyList<decimal> UnitAreasSqm);

    public static decimal GetCommonAreaSqm(decimal floorPlateSqm) =>
        decimal.Round(floorPlateSqm * CommonAreaFraction, 2, MidpointRounding.AwayFromZero);

    public static decimal GetRentableAreaSqm(decimal floorPlateSqm) =>
        decimal.Round(floorPlateSqm - GetCommonAreaSqm(floorPlateSqm), 2, MidpointRounding.AwayFromZero);

    /// <summary>Each floor uses the same gross plate area as the entered property size.</summary>
    public static IReadOnlyList<decimal> SplitPropertyAcrossFloors(decimal floorPlateSqm, int floorCount)
    {
        if (floorCount < 1)
            throw new ArgumentOutOfRangeException(nameof(floorCount), "Floor count must be at least 1.");

        var plate = decimal.Round(floorPlateSqm, 2, MidpointRounding.AwayFromZero);
        return Enumerable.Repeat(plate, floorCount).ToList();
    }

    public static IReadOnlyList<decimal> BuildDefaultUnitAreas(decimal floorPlateSqm, int unitsOnFloor) =>
        DistributeExactTotal(GetRentableAreaSqm(floorPlateSqm), unitsOnFloor);

    /// <summary>Default area for one unit when rentable space is split evenly (e.g. 465 m² ÷ 7 ≈ 66.43 m²).</summary>
    public static decimal GetDefaultUnitAreaSqm(decimal floorPlateSqm, int unitsOnFloor) =>
        unitsOnFloor < 1
            ? throw new ArgumentOutOfRangeException(nameof(unitsOnFloor))
            : decimal.Round(GetRentableAreaSqm(floorPlateSqm) / unitsOnFloor, 2, MidpointRounding.AwayFromZero);

    public static int GetMaxUnitsOnFloor(decimal floorPlateSqm) =>
        (int)Math.Floor(GetRentableAreaSqm(floorPlateSqm) / MinUnitAreaSqm);

    public static IReadOnlyList<string> ValidateUnitsOnFloor(decimal floorPlateSqm, int unitsOnFloor, int floorNumber)
    {
        var errors = new List<string>();
        if (unitsOnFloor < 1 || unitsOnFloor > 99)
        {
            errors.Add($"Floor {floorNumber}: units on floor must be between 1 and 99.");
            return errors;
        }

        var rentable = GetRentableAreaSqm(floorPlateSqm);
        var maxUnits = GetMaxUnitsOnFloor(floorPlateSqm);
        if (maxUnits < 1)
        {
            errors.Add($"Floor {floorNumber}: rentable area ({rentable} m²) is too small for any unit.");
            return errors;
        }

        if (unitsOnFloor > maxUnits)
        {
            errors.Add(
                $"Floor {floorNumber}: at most {maxUnits} unit(s) fit when each needs at least {MinUnitAreaSqm} m² " +
                $"(rentable area is {rentable} m²).");
        }

        return errors;
    }

    public static IReadOnlyList<FloorAreaPlan> BuildDefaultFloorPlans(decimal totalPropertySqm, IReadOnlyList<int> unitsPerFloor)
    {
        if (unitsPerFloor.Count == 0)
            throw new ArgumentException("At least one floor is required.", nameof(unitsPerFloor));

        var floorPlates = SplitPropertyAcrossFloors(totalPropertySqm, unitsPerFloor.Count);
        var plans = new List<FloorAreaPlan>(unitsPerFloor.Count);

        for (var i = 0; i < unitsPerFloor.Count; i++)
        {
            var floorPlate = floorPlates[i];
            var unitsOnFloor = unitsPerFloor[i];
            if (unitsOnFloor < 1 || unitsOnFloor > 99)
                throw new ArgumentOutOfRangeException(nameof(unitsPerFloor), "Units per floor must be between 1 and 99.");

            plans.Add(new FloorAreaPlan(
                i + 1,
                floorPlate,
                GetCommonAreaSqm(floorPlate),
                BuildDefaultUnitAreas(floorPlate, unitsOnFloor)));
        }

        return plans;
    }

    public static IReadOnlyList<string> ValidatePropertySize(decimal totalPropertySqm)
    {
        var errors = new List<string>();
        if (totalPropertySqm < MinPropertySizeSqm)
            errors.Add($"Total property size must be at least {MinPropertySizeSqm} m².");
        if (totalPropertySqm > MaxPropertySizeSqm)
            errors.Add($"Total property size cannot exceed {MaxPropertySizeSqm:N0} m².");
        return errors;
    }

    public static IReadOnlyList<string> ValidatePropertySize(decimal totalPropertySqm, int floorCount, IReadOnlyList<int> unitsPerFloor)
    {
        var errors = new List<string>(ValidatePropertySize(totalPropertySqm));
        if (errors.Count > 0 || floorCount < 1 || unitsPerFloor.Count == 0)
            return errors;

        var floorPlates = SplitPropertyAcrossFloors(totalPropertySqm, floorCount);
        for (var i = 0; i < unitsPerFloor.Count; i++)
        {
            var floorNumber = i + 1;
            var unitsOnFloor = unitsPerFloor[i];
            if (unitsOnFloor < 1)
                continue;

            var rentable = GetRentableAreaSqm(floorPlates[i]);
            var minRentableRequired = unitsOnFloor * MinUnitAreaSqm;
            if (rentable < minRentableRequired)
            {
                errors.Add(
                    $"Floor {floorNumber}: per-floor size is too small for {unitsOnFloor} unit(s) " +
                    $"(need at least {minRentableRequired} m² rentable; this floor has {rentable} m²). " +
                    "Increase the per-floor size or reduce units on this floor.");
            }
        }

        return errors;
    }

    public static decimal SumUnitAreas(IReadOnlyList<decimal> unitAreas) =>
        decimal.Round(unitAreas.Sum(), 2, MidpointRounding.AwayFromZero);

    public static decimal GetUnallocatedRentableSqm(decimal floorPlateSqm, IReadOnlyList<decimal> unitAreas) =>
        decimal.Round(GetRentableAreaSqm(floorPlateSqm) - SumUnitAreas(unitAreas), 2, MidpointRounding.AwayFromZero);

    public static bool IsOverRentable(decimal floorPlateSqm, IReadOnlyList<decimal> unitAreas) =>
        SumUnitAreas(unitAreas) > GetRentableAreaSqm(floorPlateSqm) + AreaToleranceSqm;

    public static bool IsUnderRentable(decimal floorPlateSqm, IReadOnlyList<decimal> unitAreas) =>
        SumUnitAreas(unitAreas) < GetRentableAreaSqm(floorPlateSqm) - AreaToleranceSqm;

    public static (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidatePropertyAreaAllocationDetailed(
        decimal totalPropertySqm,
        IReadOnlyList<(int UnitsOnFloor, IReadOnlyList<decimal> UnitAreas)> floors)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var unitsPerFloor = floors.Select(f => f.UnitsOnFloor).ToList();
        errors.AddRange(ValidatePropertySize(totalPropertySqm, floors.Count, unitsPerFloor));

        if (floors.Count == 0)
        {
            errors.Add("At least one floor is required.");
            return (errors, warnings);
        }

        if (floors.Count > 99)
            errors.Add("At most 99 floors are supported.");

        var floorPlates = SplitPropertyAcrossFloors(totalPropertySqm, floors.Count);

        for (var i = 0; i < floors.Count; i++)
        {
            var floorNumber = i + 1;
            var (unitsOnFloor, unitAreas) = floors[i];
            var floorPlate = floorPlates[i];
            var rentable = GetRentableAreaSqm(floorPlate);

            errors.AddRange(ValidateUnitsOnFloor(floorPlate, unitsOnFloor, floorNumber));

            if (unitsOnFloor < 1 || unitsOnFloor > 99)
                continue;

            if (unitAreas.Count != unitsOnFloor)
            {
                errors.Add($"Floor {floorNumber}: provide exactly {unitsOnFloor} unit area value(s).");
                continue;
            }

            for (var u = 0; u < unitAreas.Count; u++)
            {
                var area = unitAreas[u];
                if (area < MinUnitAreaSqm)
                    errors.Add($"Floor {floorNumber}, unit {u + 1}: area must be at least {MinUnitAreaSqm} m².");
                if (area > MaxUnitAreaSqm)
                    errors.Add($"Floor {floorNumber}, unit {u + 1}: area cannot exceed {MaxUnitAreaSqm:N0} m².");
            }

            var unitSum = SumUnitAreas(unitAreas);

            if (IsOverRentable(floorPlate, unitAreas))
            {
                errors.Add(
                    $"Floor {floorNumber}: unit areas total {unitSum} m² exceeds rentable space ({rentable} m²). " +
                    "Reduce a unit size or increase another unit only within the remaining budget.");
                continue;
            }

            if (IsUnderRentable(floorPlate, unitAreas))
            {
                var unallocated = GetUnallocatedRentableSqm(floorPlate, unitAreas);
                warnings.Add(
                    $"Floor {floorNumber}: {unallocated} m² of rentable space is not assigned to any unit. " +
                    "Increase unit sizes until the full rentable area is used.");
            }
        }

        return (errors, warnings);
    }

    public static IReadOnlyList<string> ValidatePropertyAreaAllocation(
        decimal totalPropertySqm,
        IReadOnlyList<(int UnitsOnFloor, IReadOnlyList<decimal> UnitAreas)> floors)
    {
        var (errors, warnings) = ValidatePropertyAreaAllocationDetailed(totalPropertySqm, floors);
        var combined = new List<string>(errors);
        combined.AddRange(warnings);
        return combined;
    }

    /// <summary>Unit areas in the same order as <see cref="PropertyCreateUnitNaming.BuildUnitNumbers"/>.</summary>
    public static IReadOnlyList<double> FlattenUnitAreasSqm(
        decimal totalPropertySqm,
        IReadOnlyList<(int UnitsOnFloor, IReadOnlyList<decimal> UnitAreas)> floors)
    {
        var (errors, warnings) = ValidatePropertyAreaAllocationDetailed(totalPropertySqm, floors);
        if (errors.Count > 0 || warnings.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors.Concat(warnings)));

        return floors
            .SelectMany(f => f.UnitAreas)
            .Select(a => (double)decimal.Round(a, 2, MidpointRounding.AwayFromZero))
            .ToList();
    }

    public static bool NearlyEqual(decimal a, decimal b) =>
        Math.Abs(a - b) <= AreaToleranceSqm;

    private static IReadOnlyList<decimal> DistributeExactTotal(decimal total, int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

        total = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
        if (total <= 0)
            throw new ArgumentOutOfRangeException(nameof(total), "Total must be positive.");

        var evenShare = decimal.Round(total / count, 2, MidpointRounding.AwayFromZero);
        var parts = new decimal[count];
        var allocated = 0m;

        for (var i = 0; i < count - 1; i++)
        {
            parts[i] = evenShare;
            allocated += evenShare;
        }

        parts[count - 1] = decimal.Round(total - allocated, 2, MidpointRounding.AwayFromZero);
        return parts;
    }
}
