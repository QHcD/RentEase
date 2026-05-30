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

    /// <summary>Unit-only custom amenities (no fixed catalog).</summary>
    public static string? JoinCustomOnly(IEnumerable<string>? customAmenities) =>
        JoinForUnit(Merge(null, customAmenities, Array.Empty<string>()).ToList());

    public static IReadOnlyList<string> ParseCommaSeparated(string? storedCommaSeparated) =>
        string.IsNullOrWhiteSpace(storedCommaSeparated)
            ? Array.Empty<string>()
            : storedCommaSeparated.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static IReadOnlyList<string> ValidateCustomAmenityList(IEnumerable<string>? customAmenities)
    {
        var errors = new List<string>();
        var list = (customAmenities ?? Enumerable.Empty<string>())
            .Select(c => c?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .ToList();

        if (list.Count > MaxCustomAmenityItems)
            errors.Add($"At most {MaxCustomAmenityItems} custom amenities are allowed.");

        if (list.Any(c => c.Length > MaxCustomAmenityItemLength))
            errors.Add($"Each custom amenity must be at most {MaxCustomAmenityItemLength} characters.");

        var joined = JoinCustomOnly(list);
        var lengthError = ValidateJoinedLength(joined);
        if (lengthError != null)
            errors.Add(lengthError);

        return errors;
    }

    /// <summary>Returns unit amenity names that already appear on the property (case-insensitive).</summary>
    public static IReadOnlyList<string> FindDuplicatesAgainstProperty(
        IEnumerable<string>? unitAmenities,
        IEnumerable<string>? propertyAmenities)
    {
        var propertySet = new HashSet<string>(
            (propertyAmenities ?? Enumerable.Empty<string>())
                .Select(a => a.Trim())
                .Where(a => a.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        if (propertySet.Count == 0)
            return Array.Empty<string>();

        return (unitAmenities ?? Enumerable.Empty<string>())
            .Select(a => a?.Trim() ?? string.Empty)
            .Where(a => a.Length > 0)
            .Where(a => propertySet.Contains(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Unit amenities must not repeat building-wide property amenities.</summary>
    public static IReadOnlyList<string> ValidateUnitAmenitiesAgainstProperty(
        IEnumerable<string>? unitAmenities,
        IEnumerable<string>? propertyAmenities)
    {
        var duplicates = FindDuplicatesAgainstProperty(unitAmenities, propertyAmenities);
        if (duplicates.Count == 0)
            return Array.Empty<string>();

        return duplicates
            .Select(d => $"'{d}' is already a property amenity — unit amenities are for extras not listed on the property.")
            .ToList();
    }

    public static string? ValidateJoinedLength(string? joined)
    {
        if (joined == null) return null;
        return joined.Length > MaxJoinedAmenitiesLength
            ? $"Amenities text cannot exceed {MaxJoinedAmenitiesLength} characters."
            : null;
    }
}
