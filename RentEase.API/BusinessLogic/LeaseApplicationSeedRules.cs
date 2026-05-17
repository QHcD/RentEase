namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Rules for seed data and for which lease applications appear on the MVC Applications / Renewal tabs.
/// </summary>
public static class LeaseApplicationSeedRules
{
    public static bool RegularPendingOrScreeningOnOccupiedUnit(
        int? parentLeaseId,
        string applicationStatus,
        string? unitAvailabilityStatus) =>
        !parentLeaseId.HasValue &&
        string.Equals(unitAvailabilityStatus, "Occupied", StringComparison.Ordinal) &&
        applicationStatus is "Pending" or "Screening";

    public static bool ApprovedApplicationStillAwaitingLeaseActivation(IEnumerable<string> leaseStatuses)
    {
        var list = leaseStatuses as IList<string> ?? leaseStatuses.ToList();
        if (list.Count == 0)
            return true;
        return list.All(s => string.Equals(s, "PendingPayment", StringComparison.Ordinal));
    }

    public static bool ShowOnLeaseApplicationPipelineTabs(
        string applicationStatus,
        IEnumerable<string> leaseStatuses)
    {
        if (!string.Equals(applicationStatus, "Approved", StringComparison.Ordinal))
            return true;
        return ApprovedApplicationStillAwaitingLeaseActivation(leaseStatuses);
    }

    public static bool HiddenFromLeaseApplicationAllFilter(string applicationStatus) =>
        applicationStatus is "Rejected" or "Canceled";
}
