namespace PropertyLeasing.LeaseApplicationLogic;

/// <summary>
/// Rules for seed data and for which lease applications appear on the MVC Applications / Renewal tabs.
/// </summary>
public static class LeaseApplicationSeedRules
{
    /// <summary>
    /// True when a non-renewal application is still in progress on a unit that is already occupied.
    /// </summary>
    public static bool RegularPendingOrScreeningOnOccupiedUnit(
        int? parentLeaseId,
        string applicationStatus,
        string? unitAvailabilityStatus) =>
        !parentLeaseId.HasValue &&
        string.Equals(unitAvailabilityStatus, "Occupied", StringComparison.Ordinal) &&
        applicationStatus is "Pending" or "Screening";

    /// <summary>
    /// Approved applications whose tenant has finished activating a lease (anything beyond PendingPayment)
    /// belong on the Leases tab, not the Applications / Renewal applications tabs.
    /// </summary>
    public static bool ApprovedApplicationStillAwaitingLeaseActivation(IEnumerable<string> leaseStatuses)
    {
        var list = leaseStatuses as IList<string> ?? leaseStatuses.ToList();
        if (list.Count == 0)
            return true;
        return list.All(s => string.Equals(s, "PendingPayment", StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether this application should appear on the Applications or Renewal applications pipeline tabs.
    /// </summary>
    public static bool ShowOnLeaseApplicationPipelineTabs(
        string applicationStatus,
        IEnumerable<string> leaseStatuses)
    {
        if (!string.Equals(applicationStatus, "Approved", StringComparison.Ordinal))
            return true;
        return ApprovedApplicationStillAwaitingLeaseActivation(leaseStatuses);
    }

    /// <summary>
    /// Rejected / Canceled rows are hidden from the Applications & Renewal tabs when the status filter is "All"
    /// so closed outcomes do not clutter the main queue (use the Rejected / Canceled pills to review them).
    /// </summary>
    public static bool HiddenFromLeaseApplicationAllFilter(string applicationStatus) =>
        applicationStatus is "Rejected" or "Canceled";
}
