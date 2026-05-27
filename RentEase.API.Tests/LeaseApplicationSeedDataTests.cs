using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class LeaseApplicationSeedDataTests
{
    [Fact]
    public void SeedDocumentTypes_MatchDocumentRules()
    {
        Assert.Equal("NationalId", LeaseApplicationDocumentRules.NationalId);
        Assert.Equal("SalaryIncome", LeaseApplicationDocumentRules.SalaryIncome);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Screening")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void SeedApplicationStatuses_AreValidPipelineStatuses(string status)
    {
        Assert.False(string.IsNullOrWhiteSpace(status));
        Assert.NotEqual("DocumentsRequired", status);
    }

    [Fact]
    public void SeedRenewalScenario_HasParentLease_NoRequiredDocuments()
    {
        Assert.True(LeaseApplicationDocumentRules.IsRenewalApplication(99));
        Assert.Empty(LeaseApplicationDocumentRules.GetRequiredDocumentTypes(99));
    }
}
