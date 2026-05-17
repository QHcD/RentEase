using System.Globalization;

namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Builds default unit numbers per floor: numeric part is <c>floorNumber * 100 + unitIndexOnFloor</c>.
/// </summary>
public static class PropertyCreateUnitNaming
{
    public static string FormatUnitNumber(string? prefix, int floorNumber, int unitIndexOnFloor)
    {
        if (floorNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(floorNumber));
        if (unitIndexOnFloor < 1 || unitIndexOnFloor > 99)
            throw new ArgumentOutOfRangeException(nameof(unitIndexOnFloor));

        var trimmedPrefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
        var numericPart = (floorNumber * 100 + unitIndexOnFloor).ToString(CultureInfo.InvariantCulture);

        return trimmedPrefix is null
            ? numericPart
            : $"{trimmedPrefix} {numericPart}";
    }

    public static IReadOnlyList<string> BuildUnitNumbers(
        IReadOnlyList<(string? Prefix, int UnitsOnFloor)> floors)
    {
        if (floors.Count > 99)
            throw new ArgumentOutOfRangeException(nameof(floors), "At most 99 floors are supported.");

        var list = new List<string>();
        for (var i = 0; i < floors.Count; i++)
        {
            var floorNumber = i + 1;
            var (prefix, count) = floors[i];
            if (count < 1 || count > 99)
                throw new ArgumentOutOfRangeException(nameof(floors), "Units per floor must be between 1 and 99.");

            for (var u = 1; u <= count; u++)
                list.Add(FormatUnitNumber(prefix, floorNumber, u));
        }

        return list;
    }
}
