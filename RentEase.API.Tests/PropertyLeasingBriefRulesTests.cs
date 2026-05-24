using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class PropertyLeasingBriefRulesTests
{
    [Fact]
    public void BriefB_DoesNotRequireTenantUnitReviews() =>
        Assert.False(PropertyLeasingBriefRules.TenantUnitReviewsRequired);

    [Fact]
    public void TenantUnitReviews_IsOutOfScopeForBriefB()
    {
        Assert.Contains("TenantUnitReviews", PropertyLeasingBriefRules.OutOfScopeFeatures);
        Assert.False(PropertyLeasingBriefRules.IsFeatureInBriefBScope("TenantUnitReviews"));
    }

    [Fact]
    public void BriefBCoreFunctionalAreas_CoversRequiredDomains()
    {
        var areas = PropertyLeasingBriefRules.BriefBCoreFunctionalAreas;
        Assert.Contains("PropertyAndUnitManagement", areas);
        Assert.Contains("LeaseLifecycle", areas);
        Assert.Contains("MaintenanceManagement", areas);
        Assert.DoesNotContain("TenantUnitReviews", areas);
    }
}
