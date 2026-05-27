using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class LeaseApplicationSeedRulesTests
{
    [Theory]
    [InlineData(null, "Pending", "Occupied", true)]
    [InlineData(null, "Screening", "Occupied", true)]
    [InlineData(null, "Pending", "Available", false)]
    [InlineData(1, "Pending", "Occupied", false)]
    public void RegularPendingOrScreeningOnOccupiedUnit(int? parentLeaseId, string status, string unitStatus, bool expected) =>
        Assert.Equal(expected, LeaseApplicationSeedRules.RegularPendingOrScreeningOnOccupiedUnit(
            parentLeaseId, status, unitStatus));

    [Fact]
    public void ApprovedWithOnlyPendingPaymentLeases_StillOnPipelineTabs() =>
        Assert.True(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs(
            "Approved", new[] { "PendingPayment" }));

    [Fact]
    public void ApprovedWithActiveLease_HiddenFromPipelineTabs() =>
        Assert.False(LeaseApplicationSeedRules.ShowOnLeaseApplicationPipelineTabs(
            "Approved", new[] { "Active" }));

    [Fact]
    public void Rejected_IsHiddenFromAllFilter() =>
        Assert.True(LeaseApplicationSeedRules.HiddenFromLeaseApplicationAllFilter("Rejected"));
}
