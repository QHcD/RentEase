namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Lease statuses that prevent deleting a unit or property (financially binding / live tenancy).
/// </summary>
public static class LeaseLifecycleGuard
{
    public static readonly HashSet<string> BlockingLeaseStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Approved",
        "PendingPayment"
    };

    public static bool IsBlockingLeaseStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && BlockingLeaseStatuses.Contains(status.Trim());
}
