using PropertyLeasing.LeaseApplicationLogic;

namespace RentEase.MVC.Tests;

public class LeaseApplicationSeedRulesTests
{
    [Theory]
    [InlineData(null, "Pending", "Occupied", true)]
    [InlineData(null, "Screening", "Occupied", true)]
    [InlineData(null, "Approved", "Occupied", false)]
    [InlineData(null, "Pending", "Available", false)]
    [InlineData(7, "Pending", "Occupied", false)]
    [InlineData(7, "Screening", "Occupied", false)]
    public void RegularPendingOrScreeningOnOccupiedUnit_expected(
        int? parentLeaseId,
        string status,
        string unitAvail,
        bool expected)
    {
        Assert.Equal(expected,
            LeaseApplicationSeedRules.RegularPendingOrScreeningOnOccupiedUnit(
                parentLeaseId, status, unitAvail));
    }

    [Fact]
    public void RegularPendingOrScreeningOnOccupiedUnit_null_unit_treats_as_not_occupied()
    {
        Assert.False(LeaseApplicationSeedRules.RegularPendingOrScreeningOnOccupiedUnit(
            null, "Pending", null));
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Screening", true)]
    [InlineData("Rejected", true)]
    [InlineData("Approved", false)]
    public void ShowOnLeaseApplicationPipelineTabs_non_approved_always_true(string status, bool expected)
    {
        Assert.Equal(expected,
            LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs(status, ["Active"]));
        if (status != "Approved")
            Assert.True(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs(status, []));
    }

    [Theory]
    [InlineData("Rejected", true)]
    [InlineData("Canceled", true)]
    [InlineData("Pending", false)]
    [InlineData("Approved", false)]
    public void HiddenFromLeaseApplicationAllFilter_expected(string status, bool hidden)
    {
        Assert.Equal(hidden, LeaseApplicationSeedRules.HiddenFromLeaseApplicationAllFilter(status));
    }

    [Fact]
    public void ShowOnLeaseApplicationPipelineTabs_approved_pending_payment_only_true()
    {
        Assert.True(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs("Approved",
            ["PendingPayment"]));
        Assert.False(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs("Approved",
            ["Active"]));
        Assert.False(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs("Approved",
            ["PendingPayment", "Active"]));
        Assert.True(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs("Approved",
            Array.Empty<string>()));
    }
}
