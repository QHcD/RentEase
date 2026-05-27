using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class LeaseApplicationIndexPartitionerTests
{
    [Fact]
    public void ApplicationStatusTabKeys_DoesNotIncludeDocumentsRequired()
    {
        Assert.DoesNotContain(
            LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired,
            LeaseApplicationIndexPartitioner.ApplicationStatusTabKeys);
        Assert.Contains("Screening", LeaseApplicationIndexPartitioner.ApplicationStatusTabKeys);
    }

    [Fact]
    public void NormalizeStatusTabKey_MapsLegacyDocumentsRequiredToScreening()
    {
        Assert.Equal(
            LeaseApplicationDocumentRules.ApplicationStatusScreening,
            LeaseApplicationIndexPartitioner.NormalizeStatusTabKey(
                LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired));
        Assert.Equal("Pending", LeaseApplicationIndexPartitioner.NormalizeStatusTabKey("Pending"));
    }

    [Fact]
    public void MatchesStatusTabFilter_LegacyDocumentsRequiredMatchesScreeningTab()
    {
        Assert.True(LeaseApplicationIndexPartitioner.MatchesStatusTabFilter(
            LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired,
            LeaseApplicationDocumentRules.ApplicationStatusScreening));
        Assert.False(LeaseApplicationIndexPartitioner.MatchesStatusTabFilter(
            LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired,
            "Pending"));
    }

    [Fact]
    public void BuildStatusCounts_GroupsLegacyDocumentsRequiredUnderScreening()
    {
        var items = new[]
        {
            new { Status = "Screening" },
            new { Status = LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired },
            new { Status = "Pending" }
        };

        var counts = LeaseApplicationIndexPartitioner.BuildStatusCounts(
            items,
            x => x.Status,
            LeaseApplicationIndexPartitioner.ApplicationStatusTabKeys);

        Assert.Equal(2, counts["Screening"]);
        Assert.Equal(1, counts["Pending"]);
        Assert.False(counts.ContainsKey(LeaseApplicationDocumentRules.ApplicationStatusDocumentsRequired));
    }
}
