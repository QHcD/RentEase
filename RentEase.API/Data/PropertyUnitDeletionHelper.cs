using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;

namespace PropertyLeasing.API.Data;

/// <summary>
/// Deletes units and dependent rows so properties can be removed without FK violations.
/// Blocks deletion when any selected unit has a blocking lease status.
/// </summary>
public static class PropertyUnitDeletionHelper
{
    public const string BlockingLeaseUserMessage =
        "Cannot delete: one or more units have an active lease, pending payment, or an approved lease awaiting activation.";

    /// <summary>Returns property IDs that contain at least one unit with a blocking lease.</summary>
    public static async Task<HashSet<int>> GetPropertyIdsWithBlockingLeasesAsync(
        PropertyLeasingDbContext db,
        CancellationToken ct = default)
    {
        var ids = await (
                from l in db.Leases.AsNoTracking()
                where LeaseLifecycleGuard.BlockingLeaseStatuses.Contains(l.Status)
                join a in db.LeaseApplications.AsNoTracking() on l.ApplicationId equals a.ApplicationId
                join u in db.Units.AsNoTracking() on a.UnitId equals u.UnitId
                select u.PropertyId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <summary>Unit IDs under this property that have a blocking lease.</summary>
    public static async Task<HashSet<int>> GetBlockingUnitIdsForPropertyAsync(
        PropertyLeasingDbContext db,
        int propertyId,
        CancellationToken ct = default)
    {
        var unitIds = await db.Units.AsNoTracking()
            .Where(u => u.PropertyId == propertyId)
            .Select(u => u.UnitId)
            .ToListAsync(ct);

        if (unitIds.Count == 0)
            return new HashSet<int>();

        var blocked = await (
                from l in db.Leases.AsNoTracking()
                where LeaseLifecycleGuard.BlockingLeaseStatuses.Contains(l.Status)
                join a in db.LeaseApplications.AsNoTracking() on l.ApplicationId equals a.ApplicationId
                where unitIds.Contains(a.UnitId)
                select a.UnitId)
            .Distinct()
            .ToListAsync(ct);

        return blocked.ToHashSet();
    }

    public static Task<bool> HasBlockingLeaseOnUnitsAsync(
        PropertyLeasingDbContext db,
        IReadOnlyCollection<int> unitIds,
        CancellationToken ct = default)
    {
        if (unitIds.Count == 0)
            return Task.FromResult(false);

        var idSet = unitIds as HashSet<int> ?? unitIds.ToHashSet();
        return db.Leases.AsNoTracking()
            .Where(l => LeaseLifecycleGuard.BlockingLeaseStatuses.Contains(l.Status))
            .Join(db.LeaseApplications.AsNoTracking(),
                l => l.ApplicationId,
                a => a.ApplicationId,
                (_, a) => a.UnitId)
            .AnyAsync(uid => idSet.Contains(uid), ct);
    }

    /// <summary>
    /// Removes units (and related lease/application/maintenance/feedback data). Does not remove the property row.
    /// </summary>
    public static async Task<(bool Ok, string? Error)> TryCascadeDeleteUnitsAsync(
        PropertyLeasingDbContext db,
        IReadOnlyList<int> unitIds,
        CancellationToken ct = default)
    {
        if (unitIds.Count == 0)
            return (true, null);

        var idHash = unitIds.ToHashSet();

        if (await HasBlockingLeaseOnUnitsAsync(db, unitIds, ct))
            return (false, BlockingLeaseUserMessage);

        var applicationIds = await db.LeaseApplications
            .Where(a => idHash.Contains(a.UnitId))
            .Select(a => a.ApplicationId)
            .ToListAsync(ct);

        var leaseIds = await db.Leases
            .Where(l => applicationIds.Contains(l.ApplicationId))
            .Select(l => l.LeaseId)
            .ToListAsync(ct);

        var leaseIdHash = leaseIds.ToHashSet();

        var leasesRenewRefs = await db.Leases
            .Where(l => l.RenewLeaseApplicationId != null && applicationIds.Contains(l.RenewLeaseApplicationId.Value))
            .ToListAsync(ct);
        foreach (var l in leasesRenewRefs)
            l.RenewLeaseApplicationId = null;

        var appsParentClear = await db.LeaseApplications
            .Where(a => a.ParentLeaseId != null && leaseIdHash.Contains(a.ParentLeaseId.Value))
            .ToListAsync(ct);
        foreach (var a in appsParentClear)
            a.ParentLeaseId = null;

        await db.SaveChangesAsync(ct);

        var feedbacks = await db.Feedbacks.Where(f => idHash.Contains(f.UnitId)).ToListAsync(ct);
        db.Feedbacks.RemoveRange(feedbacks);

        var maintRequests = await db.MaintenanceRequests
            .Include(m => m.StatusHistory)
            .Include(m => m.RequestLogs)
            .Where(m => idHash.Contains(m.UnitId))
            .ToListAsync(ct);

        foreach (var m in maintRequests)
        {
            db.MaintenanceStatusHistories.RemoveRange(m.StatusHistory);
            db.MaintenanceRequestLogs.RemoveRange(m.RequestLogs);
        }

        db.MaintenanceRequests.RemoveRange(maintRequests);
        await db.SaveChangesAsync(ct);

        var payments = await db.PaymentRecords.Where(p => leaseIds.Contains(p.LeaseId)).ToListAsync(ct);
        db.PaymentRecords.RemoveRange(payments);

        var leaseLogs = await db.LeaseLogs.Where(log => leaseIds.Contains(log.LeaseId)).ToListAsync(ct);
        db.LeaseLogs.RemoveRange(leaseLogs);

        var leases = await db.Leases.Where(l => leaseIds.Contains(l.LeaseId)).ToListAsync(ct);
        var terminationIds = leases
            .Where(l => l.TerminationId != null)
            .Select(l => l.TerminationId!.Value)
            .Distinct()
            .ToList();

        foreach (var l in leases)
        {
            l.TerminationId = null;
            l.RenewLeaseApplicationId = null;
        }

        db.Leases.RemoveRange(leases);
        await db.SaveChangesAsync(ct);

        if (terminationIds.Count > 0)
        {
            var terms = await db.Terminations.Where(t => terminationIds.Contains(t.TerminationId)).ToListAsync(ct);
            db.Terminations.RemoveRange(terms);
            await db.SaveChangesAsync(ct);
        }

        var docs = await db.Documents
            .Where(d => d.ApplicationId != null && applicationIds.Contains(d.ApplicationId.Value))
            .ToListAsync(ct);
        db.Documents.RemoveRange(docs);

        var applicationLogs = await db.LeaseApplicationLogs
            .Where(log => applicationIds.Contains(log.ApplicationId))
            .ToListAsync(ct);
        db.LeaseApplicationLogs.RemoveRange(applicationLogs);

        var apps = await db.LeaseApplications.Where(a => applicationIds.Contains(a.ApplicationId)).ToListAsync(ct);
        db.LeaseApplications.RemoveRange(apps);

        await db.SaveChangesAsync(ct);

        var units = await db.Units.Where(u => idHash.Contains(u.UnitId)).ToListAsync(ct);
        db.Units.RemoveRange(units);

        await db.SaveChangesAsync(ct);

        return (true, null);
    }
}
