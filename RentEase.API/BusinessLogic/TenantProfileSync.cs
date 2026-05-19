namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Keeps PropertyLeasing <c>User</c> contact fields aligned with ASP.NET Identity (tenant profile source of truth).
/// </summary>
public static class TenantProfileSync
{
    public const string DisplayNameClaimType = "DisplayName";

    public sealed record ProfileSnapshot(string FullName, string Email, string? Phone);

    /// <summary>Navbar label: full name first, then email, then username.</summary>
    public static string ResolveNavbarDisplayName(string? fullName, string? email, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName.Trim();
        if (!string.IsNullOrWhiteSpace(email))
            return email.Trim();
        return string.IsNullOrWhiteSpace(userName) ? "User" : userName.Trim();
    }

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(c => char.IsDigit(c)).ToArray());
    }

    public static bool ProfilesMatch(ProfileSnapshot identity, ProfileSnapshot app) =>
        string.Equals(identity.FullName.Trim(), app.FullName.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(identity.Email.Trim(), app.Email.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(NormalizePhone(identity.Phone), NormalizePhone(app.Phone), StringComparison.Ordinal);

    /// <summary>Returns true when the app user record was updated.</summary>
    public static bool ApplyIdentityToAppUser(
        string identityFullName,
        string identityEmail,
        string? identityPhone,
        Action<string> setFullName,
        Action<string> setEmail,
        Action<string?> setPhone,
        ProfileSnapshot currentApp)
    {
        var identity = new ProfileSnapshot(identityFullName, identityEmail, identityPhone);
        if (ProfilesMatch(identity, currentApp))
            return false;

        setFullName(identityFullName);
        setEmail(identityEmail);
        setPhone(identityPhone);
        return true;
    }
}

/// <summary>Canonical tenant/manager profiles used by seed data and unity tests.</summary>
public static class TenantProfileCatalog
{
    public sealed record Entry(string FullName, string Phone);

    public static readonly IReadOnlyDictionary<string, Entry> ByEmail =
        new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
        {
            ["manager@propleasing.com"] = new("Ahmed Al-Mansoori", "+97317171001"),
            ["tenant1@example.com"]     = new("Sara Al-Khalifa", "+97333112233"),
            ["tenant2@example.com"]     = new("Mohammed Al-Baker", "+97333224455"),
            ["tenant3@example.com"]     = new("Noor Ibrahim", "+97333667788"),
            ["tenant4@example.com"]     = new("Fatima Al-Zayani", "+97333991122"),
            ["tenant5@example.com"]     = new("Omar Al-Rumaihi", "+97333556677"),
        };

    public static bool TryGetByEmail(string email, out Entry entry) =>
        ByEmail.TryGetValue(email, out entry!);
}
