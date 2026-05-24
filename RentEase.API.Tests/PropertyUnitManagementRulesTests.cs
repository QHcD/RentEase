using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class PropertyUnitManagementRulesTests
{
    [Fact]
    public void AllowManagerUnitDeletion_IsFalse() =>
        Assert.False(PropertyUnitManagementRules.AllowManagerUnitDeletion);

    [Fact]
    public void CanManagerDeleteUnit_ReturnsFalse() =>
        Assert.False(PropertyUnitManagementRules.CanManagerDeleteUnit());
}
