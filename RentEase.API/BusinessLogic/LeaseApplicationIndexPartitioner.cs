namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Splits lease applications into regular vs renewal (ParentLeaseId) for the Applications index tabs.
/// </summary>
public static class LeaseApplicationIndexPartitioner
{
    /// <summary>Status sub-tabs on the Applications index (DocumentsRequired is legacy — folded into Screening).</summary>
    public static readonly string[] ApplicationStatusTabKeys =
        { "All", "Pending", "Screening", "Approved", "Rejected", "Canceled" };

    public static bool IsRenewalApplication(int? parentLeaseId) => parentLeaseId.HasValue;

    /// <summary>Maps legacy <see cref="LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired"/> to Screening for UI tabs.</summary>
    public static string NormalizeStatusTabKey(string status) =>
        string.Equals(status, LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired, StringComparison.OrdinalIgnoreCase)
            ? LeaseApplicationDocumentRules.ApplicationStatusScreening
            : status;

    public static bool MatchesStatusTabFilter(string applicationStatus, string tabFilter) =>
        string.Equals(NormalizeStatusTabKey(applicationStatus), tabFilter, StringComparison.OrdinalIgnoreCase);

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
                : items.Count(i => MatchesStatusTabFilter(statusSelector(i), key));
        }

        return dict;
    }
}
