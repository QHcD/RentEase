namespace PropertyLeasing.BusinessLogic;

using System.Globalization;

/// <summary>
/// Resolves monthly rent for new units: unit value → floor value → property default.
/// </summary>
public static class PropertyMonthlyRentRules
{
    /// <summary>Minimum monthly rent in Bahraini Dinar (BD).</summary>
    public const decimal MinMonthlyRentBd = 10m;

    public sealed record FloorMonthlyRentInput(
        int UnitsOnFloor,
        decimal? FloorMonthlyRent,
        IReadOnlyList<decimal?>? UnitMonthlyRents);

    public static decimal? NormalizeOptionalRent(decimal? value) =>
        value is >= MinMonthlyRentBd ? value : null;

    public static string MinMonthlyRentMessage =>
        $"Minimum monthly rent is BD {MinMonthlyRentBd.ToString("N3", CultureInfo.InvariantCulture)}.";

    public static decimal? ResolveUnitMonthlyRent(
        decimal? propertyMonthlyRent,
        decimal? floorMonthlyRent,
        decimal? unitMonthlyRent)
    {
        var unit = NormalizeOptionalRent(unitMonthlyRent);
        if (unit.HasValue) return unit;

        var floor = NormalizeOptionalRent(floorMonthlyRent);
        if (floor.HasValue) return floor;

        return NormalizeOptionalRent(propertyMonthlyRent);
    }

    public static bool HasAnyRentSpecified(
        decimal? propertyMonthlyRent,
        IReadOnlyList<FloorMonthlyRentInput> floors)
    {
        if (NormalizeOptionalRent(propertyMonthlyRent).HasValue)
            return true;

        foreach (var floor in floors)
        {
            if (NormalizeOptionalRent(floor.FloorMonthlyRent).HasValue)
                return true;

            if (floor.UnitMonthlyRents == null)
                continue;

            foreach (var unitRent in floor.UnitMonthlyRents)
            {
                if (NormalizeOptionalRent(unitRent).HasValue)
                    return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> ValidateMonthlyRentLayout(
        decimal? propertyMonthlyRent,
        IReadOnlyList<FloorMonthlyRentInput> floors)
    {
        var errors = new List<string>();
        var propertyRent = NormalizeOptionalRent(propertyMonthlyRent);

        if (propertyMonthlyRent.HasValue && !propertyRent.HasValue)
            errors.Add($"Property default monthly rent: {MinMonthlyRentMessage}");

        if (floors.Count == 0)
            return errors;

        for (var floorIdx = 0; floorIdx < floors.Count; floorIdx++)
        {
            var floor = floors[floorIdx];
            if (floor.FloorMonthlyRent.HasValue && !NormalizeOptionalRent(floor.FloorMonthlyRent).HasValue)
                errors.Add($"Floor {floorIdx + 1}: {MinMonthlyRentMessage}");

            for (var unitIdx = 0; unitIdx < floor.UnitsOnFloor; unitIdx++)
            {
                decimal? unitRaw = floor.UnitMonthlyRents != null && unitIdx < floor.UnitMonthlyRents.Count
                    ? floor.UnitMonthlyRents[unitIdx]
                    : null;

                if (unitRaw.HasValue && !NormalizeOptionalRent(unitRaw).HasValue)
                    errors.Add($"Floor {floorIdx + 1}, unit {unitIdx + 1}: {MinMonthlyRentMessage}");
            }
        }

        if (!HasAnyRentSpecified(propertyRent, floors))
        {
            if (errors.Count == 0)
                errors.Add("Enter a monthly rent at property, floor, or unit level so every unit can be priced.");
            return errors;
        }

        for (var floorIdx = 0; floorIdx < floors.Count; floorIdx++)
        {
            var floor = floors[floorIdx];

            for (var unitIdx = 0; unitIdx < floor.UnitsOnFloor; unitIdx++)
            {
                decimal? unitRaw = floor.UnitMonthlyRents != null && unitIdx < floor.UnitMonthlyRents.Count
                    ? floor.UnitMonthlyRents[unitIdx]
                    : null;

                if (unitRaw.HasValue && !NormalizeOptionalRent(unitRaw).HasValue)
                    continue;

                var resolved = ResolveUnitMonthlyRent(
                    propertyRent,
                    floor.FloorMonthlyRent,
                    unitRaw);

                if (!resolved.HasValue)
                {
                    errors.Add(
                        $"Floor {floorIdx + 1}, unit {unitIdx + 1}: no monthly rent — set a unit, floor, or property default.");
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<decimal> ResolveAllUnitRents(
        decimal? propertyMonthlyRent,
        IReadOnlyList<FloorMonthlyRentInput> floors)
    {
        var propertyRent = NormalizeOptionalRent(propertyMonthlyRent);
        var rents = new List<decimal>();

        foreach (var floor in floors)
        {
            for (var unitIdx = 0; unitIdx < floor.UnitsOnFloor; unitIdx++)
            {
                decimal? unitRaw = floor.UnitMonthlyRents != null && unitIdx < floor.UnitMonthlyRents.Count
                    ? floor.UnitMonthlyRents[unitIdx]
                    : null;

                var resolved = ResolveUnitMonthlyRent(propertyRent, floor.FloorMonthlyRent, unitRaw)
                    ?? throw new InvalidOperationException(
                        $"Could not resolve monthly rent for a unit (floor rent={floor.FloorMonthlyRent}, unit rent={unitRaw}).");

                rents.Add(resolved);
            }
        }

        return rents;
    }
}
