namespace PropertyLeasing.LeaseApplicationLogic;

/// <summary>
/// Splits lease applications into regular vs renewal (ParentLeaseId)
/// for the Applications index tabs.
/// </summary>
public static class LeaseApplicationIndexPartitioner
{
    public static bool IsRenewalApplication(int? parentLeaseId) => parentLeaseId.HasValue;

    public static (List<T> Regular, List<T> Renewals) PartitionByRenewal<T>(
        IEnumerable<T> items,
        Func<T, int?> parentLeaseIdSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(parentLeaseIdSelector);

        var regular = new List<T>();
        var renewals = new List<T>();
        foreach (var item in items)
        {
            if (parentLeaseIdSelector(item).HasValue)
                renewals.Add(item);
            else
                regular.Add(item);
        }

        return (regular, renewals);
    }

    /// <summary>
    /// Status counts for application sub-tabs. Include <c>All</c> for total count.
    /// </summary>
    public static Dictionary<string, int> BuildStatusCounts<T>(
        IReadOnlyCollection<T> items,
        Func<T, string> statusSelector,
        IReadOnlyList<string> statusKeys)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(statusSelector);
        ArgumentNullException.ThrowIfNull(statusKeys);

        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in statusKeys)
        {
            dict[key] = key == "All"
                ? items.Count
                : items.Count(i => statusSelector(i) == key);
        }

        return dict;
    }
}
