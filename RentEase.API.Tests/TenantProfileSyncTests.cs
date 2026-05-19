using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class TenantProfileSyncTests
{
    [Fact]
    public void ProfilesMatch_WhenSameContact_ReturnsTrue()
    {
        var identity = new TenantProfileSync.ProfileSnapshot("Noor Ibrahim", "tenant3@example.com", "+97333667788");
        var app      = new TenantProfileSync.ProfileSnapshot("Noor Ibrahim", "tenant3@example.com", "+973 3366 7788");

        Assert.True(TenantProfileSync.ProfilesMatch(identity, app));
    }

    [Fact]
    public void ProfilesMatch_WhenNameDiffers_ReturnsFalse()
    {
        var identity = new TenantProfileSync.ProfileSnapshot("Noor Ibrahim", "tenant3@example.com", "+97333667788");
        var app      = new TenantProfileSync.ProfileSnapshot("Fatima Nasser", "tenant3@example.com", "+97333000006");

        Assert.False(TenantProfileSync.ProfilesMatch(identity, app));
    }

    [Fact]
    public void ApplyIdentityToAppUser_UpdatesMismatchedFields()
    {
        var fullName = "Fatima Nasser";
        var email    = "tenant3@example.com";
        string? phone = "+973 3300 0006";

        var changed = TenantProfileSync.ApplyIdentityToAppUser(
            "Noor Ibrahim",
            "tenant3@example.com",
            "+97333667788",
            v => fullName = v,
            v => email = v,
            v => phone = v,
            new TenantProfileSync.ProfileSnapshot("Fatima Nasser", "tenant3@example.com", "+973 3300 0006"));

        Assert.True(changed);
        Assert.Equal("Noor Ibrahim", fullName);
        Assert.Equal("+97333667788", phone);
    }

    [Fact]
    public void ApplyIdentityToAppUser_WhenAlreadySynced_ReturnsFalse()
    {
        var fullName = "Noor Ibrahim";
        var email    = "tenant3@example.com";
        string? phone = "+97333667788";

        var changed = TenantProfileSync.ApplyIdentityToAppUser(
            "Noor Ibrahim",
            "tenant3@example.com",
            "+97333667788",
            v => fullName = v,
            v => email = v,
            v => phone = v,
            new TenantProfileSync.ProfileSnapshot(fullName, email, phone));

        Assert.False(changed);
    }

    [Fact]
    public void TenantProfileCatalog_Tenant3_MatchesIdentitySeed()
    {
        Assert.True(TenantProfileCatalog.TryGetByEmail("tenant3@example.com", out var entry));
        Assert.Equal("Noor Ibrahim", entry.FullName);
        Assert.Equal("+97333667788", entry.Phone);
    }

    [Theory]
    [InlineData("Noor Ibrahim", "tenant3@example.com", "tenant3@example.com", "Noor Ibrahim")]
    [InlineData("", "tenant3@example.com", "tenant3@example.com", "tenant3@example.com")]
    [InlineData(null, null, "tenant3@example.com", "tenant3@example.com")]
    public void ResolveNavbarDisplayName_PrefersFullName(
        string? fullName, string? email, string? userName, string expected)
    {
        Assert.Equal(expected, TenantProfileSync.ResolveNavbarDisplayName(fullName, email, userName));
    }

    [Fact]
    public void ResolveNavbarDisplayName_Tenant3Catalog_ReturnsFullNameNotEmail()
    {
        Assert.True(TenantProfileCatalog.TryGetByEmail("tenant3@example.com", out var entry));
        var label = TenantProfileSync.ResolveNavbarDisplayName(
            entry.FullName, "tenant3@example.com", "tenant3@example.com");
        Assert.Equal("Noor Ibrahim", label);
        Assert.NotEqual("tenant3@example.com", label);
    }

    [Fact]
    public void TenantProfileCatalog_Tenant3_DoesNotMatchLegacySqlSeedName()
    {
        Assert.True(TenantProfileCatalog.TryGetByEmail("tenant3@example.com", out var entry));
        Assert.NotEqual("Fatima Nasser", entry.FullName);
    }
}
