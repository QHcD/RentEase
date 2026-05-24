using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class PropertyManagerAccessRulesTests
{
    [Theory]
    [InlineData("PropertyManager", true)]
    [InlineData("propertymanager", true)]
    [InlineData("Tenant", false)]
    [InlineData("MaintenanceStaff", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsPropertyManager_OnlyManagerRole(string? role, bool expected) =>
        Assert.Equal(expected, PropertyManagerAccessRules.IsPropertyManager(role));

    [Fact]
    public void ManagerOnlyPropertyActions_IncludesManageAndAdd()
    {
        Assert.Contains("Manage", PropertyManagerAccessRules.ManagerOnlyPropertyActions);
        Assert.Contains("Add", PropertyManagerAccessRules.ManagerOnlyPropertyActions);
    }

    [Fact]
    public void DisallowedManagerPropertyActions_IncludesDeleteUnit()
    {
        Assert.Contains("DeleteUnit", PropertyManagerAccessRules.DisallowedManagerPropertyActions);
        Assert.DoesNotContain("DeleteUnit", PropertyManagerAccessRules.ManagerOnlyPropertyActions);
    }
}
