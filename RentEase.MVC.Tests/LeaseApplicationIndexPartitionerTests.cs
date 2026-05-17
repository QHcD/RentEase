using PropertyLeasing.LeaseApplicationLogic;

namespace RentEase.MVC.Tests;

public class LeaseApplicationIndexPartitionerTests
{
    private sealed class AppRow
    {
        public required string Status { get; init; }
        public int? ParentLeaseId { get; init; }
    }

    [Fact]
    public void IsRenewalApplication_false_when_parent_null()
    {
        Assert.False(LeaseApplicationIndexPartitioner.IsRenewalApplication(null));
    }

    [Fact]
    public void IsRenewalApplication_true_when_parent_set()
    {
        Assert.True(LeaseApplicationIndexPartitioner.IsRenewalApplication(100));
    }

    [Fact]
    public void PartitionByRenewal_splits_rows_by_parent_lease_id()
    {
        var rows = new[]
        {
            new AppRow { Status = "Pending", ParentLeaseId = null },
            new AppRow { Status = "Pending", ParentLeaseId = 42 },
            new AppRow { Status = "Screening", ParentLeaseId = null },
        };

        var (regular, renewals) =
            LeaseApplicationIndexPartitioner.PartitionByRenewal(rows, r => r.ParentLeaseId);

        Assert.Equal(2, regular.Count);
        Assert.Single(renewals);
        Assert.Contains(renewals, r => r.ParentLeaseId == 42);
        Assert.All(regular, r => Assert.Null(r.ParentLeaseId));
    }

    [Fact]
    public void PartitionByRenewal_null_items_throws()
    {
        IEnumerable<AppRow>? nullRows = null;
        Assert.Throws<ArgumentNullException>(() =>
            LeaseApplicationIndexPartitioner.PartitionByRenewal(nullRows!, r => r.ParentLeaseId));
    }

    [Fact]
    public void PartitionByRenewal_null_selector_throws()
    {
        var rows = new[] { new AppRow { Status = "Pending", ParentLeaseId = null } };
        Assert.Throws<ArgumentNullException>(() =>
            LeaseApplicationIndexPartitioner.PartitionByRenewal(rows, null!));
    }

    [Fact]
    public void BuildStatusCounts_counts_All_and_each_status()
    {
        var keys = new[] { "All", "Pending", "Screening", "Approved" };
        var rows = new[]
        {
            new AppRow { Status = "Pending", ParentLeaseId = null },
            new AppRow { Status = "Pending", ParentLeaseId = null },
            new AppRow { Status = "Approved", ParentLeaseId = null },
        };

        var counts = LeaseApplicationIndexPartitioner.BuildStatusCounts(
            rows,
            r => r.Status,
            keys);

        Assert.Equal(3, counts["All"]);
        Assert.Equal(2, counts["Pending"]);
        Assert.Equal(0, counts["Screening"]);
        Assert.Equal(1, counts["Approved"]);
    }

    [Fact]
    public void BuildStatusCounts_null_items_throws()
    {
        IReadOnlyCollection<AppRow>? nullRows = null;
        Assert.Throws<ArgumentNullException>(() =>
            LeaseApplicationIndexPartitioner.BuildStatusCounts(nullRows!, r => r.Status,
                new[] { "All", "Pending" }));
    }
}
