namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Merges manager-selected fixed amenities with optional custom strings for persistence on units.
/// </summary>
public static class PropertyAmenitySelection
{
    public const int MaxJoinedAmenitiesLength = 250;
    public const int MaxCustomAmenityItems = 20;
    public const int MaxCustomAmenityItemLength = 80;

    /// <summary>
    /// Parses a comma-separated amenities string (as stored on units) into fixed selections and custom rows for editing.
    /// Fixed labels match canonical entries case-insensitively; emitted fixed names use canonical casing.
    /// </summary>
    public static (List<string> SelectedFixed, List<string> CustomAmenities) SplitFromStoredString(
        string? storedCommaSeparated,
        IReadOnlyList<string> canonicalFixedOrder)
    {
        var tokens = string.IsNullOrWhiteSpace(storedCommaSeparated)
            ? new List<string>()
            : storedCommaSeparated.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        var selectedFixed = new List<string>();
        foreach (var name in canonicalFixedOrder)
        {
            if (tokens.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase)))
                selectedFixed.Add(name);
        }

        var customs = new List<string>();
        foreach (var t in tokens)
        {
            var isCanonical = canonicalFixedOrder.Any(f =>
                string.Equals(f, t, StringComparison.OrdinalIgnoreCase));
            if (isCanonical) continue;
            if (!customs.Any(c => string.Equals(c, t, StringComparison.OrdinalIgnoreCase)))
                customs.Add(t);
        }

        return (selectedFixed, customs);
    }

    public static IReadOnlyList<string> Merge(
        IEnumerable<string>? selectedFixed,
        IEnumerable<string>? customAmenities,
        IReadOnlyList<string> canonicalFixedOrder)
    {
        var allowed = new HashSet<string>(canonicalFixedOrder, StringComparer.Ordinal);
        var chosenFixed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in selectedFixed ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var t = s.Trim();
            if (allowed.Contains(t))
                chosenFixed.Add(t);
        }

        var result = new List<string>();
        foreach (var name in canonicalFixedOrder)
        {
            if (chosenFixed.Contains(name))
                result.Add(name);
        }

        var seen = new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);
        foreach (var raw in customAmenities ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var t = raw.Trim();
            if (t.Length > MaxCustomAmenityItemLength)
                continue;

            if (seen.Contains(t)) continue;

            var clashesFixed = canonicalFixedOrder.Any(f =>
                string.Equals(f, t, StringComparison.OrdinalIgnoreCase));
            if (clashesFixed) continue;

            result.Add(t);
            seen.Add(t);
        }

        return result;
    }

    public static string? JoinForUnit(IReadOnlyList<string> merged) =>
        merged.Count > 0 ? string.Join(", ", merged) : null;

    public static string? ValidateJoinedLength(string? joined)
    {
        if (joined == null) return null;
        return joined.Length > MaxJoinedAmenitiesLength
            ? $"Amenities text cannot exceed {MaxJoinedAmenitiesLength} characters."
            : null;
    }
}
