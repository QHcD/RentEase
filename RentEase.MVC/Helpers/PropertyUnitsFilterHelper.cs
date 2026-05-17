namespace PropertyLeasing.MVC.Helpers;

public static class PropertyUnitsFilterHelper
{
    public static readonly string[] AvailabilityOptions = ["Available", "Occupied", "UnderMaintenance"];
    public static readonly string[] UnitTypeOptions = ["Apartment", "Studio", "Office", "Shop"];

    /// <summary>
    /// Parses ?avail= (comma-separated), avail=all, or legacy ?availability=.
    /// When nothing is specified: default is Available-only (first visit).
    /// </summary>
    public static (bool showAllAvail, List<string> availStatuses) ParseAvailability(IQueryCollection query)
    {
        var availRaw = query["avail"].FirstOrDefault();
        var legacy = query["availability"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(availRaw))
        {
            if (string.Equals(availRaw.Trim(), "all", StringComparison.OrdinalIgnoreCase))
                return (true, []);

            var list = availRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => AvailabilityOptions.Contains(s, StringComparer.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            var ordered = AvailabilityOptions.Where(list.Contains).ToList();
            // Explicit but invalid / empty → treat as all statuses
            if (ordered.Count == 0)
                return (true, []);

            return (false, ordered);
        }

        if (!string.IsNullOrWhiteSpace(legacy))
        {
            if (string.Equals(legacy.Trim(), "All", StringComparison.OrdinalIgnoreCase))
                return (true, []);

            var v = legacy.Trim();
            if (AvailabilityOptions.Contains(v, StringComparer.Ordinal))
                return (false, [v]);
        }

        return (false, ["Available"]);
    }

    /// <summary>
    /// Parses ?types= (comma-separated) or legacy single ?unitType=.
    /// Empty list means no unit-type filter.
    /// </summary>
    public static List<string> ParseUnitTypes(IQueryCollection query)
    {
        var typesRaw = query["types"].FirstOrDefault();
        var legacy = query["unitType"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(typesRaw))
        {
            var set = typesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => UnitTypeOptions.Contains(t, StringComparer.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            return UnitTypeOptions.Where(set.Contains).ToList();
        }

        if (!string.IsNullOrWhiteSpace(legacy))
        {
            var v = legacy.Trim();
            if (UnitTypeOptions.Contains(v, StringComparer.Ordinal))
                return [v];
        }

        return [];
    }

    /// <summary>
    /// Click toggles one availability chip. Empty result means "all statuses".
    /// </summary>
    public static List<string> ToggleAvailability(bool showAllAvail, IReadOnlyList<string> current, string toggle)
    {
        if (showAllAvail)
            return [toggle];

        var set = new HashSet<string>(current, StringComparer.Ordinal);
        if (set.Contains(toggle))
            set.Remove(toggle);
        else
            set.Add(toggle);

        return AvailabilityOptions.Where(set.Contains).ToList();
    }

    /// <summary>
    /// Click toggles one unit-type chip. Empty result means any type.
    /// </summary>
    public static List<string> ToggleUnitType(IReadOnlyList<string> current, string toggle)
    {
        var set = new HashSet<string>(current, StringComparer.Ordinal);
        if (set.Contains(toggle))
            set.Remove(toggle);
        else
            set.Add(toggle);

        return UnitTypeOptions.Where(set.Contains).ToList();
    }

    public static string AvailQueryValue(bool showAllAvail, IReadOnlyList<string> statuses)
    {
        if (showAllAvail || statuses.Count == 0)
            return "all";

        return string.Join(",", statuses);
    }

    public static string? TypesQueryValue(IReadOnlyList<string> types) =>
        types.Count == 0 ? null : string.Join(",", types);
}
