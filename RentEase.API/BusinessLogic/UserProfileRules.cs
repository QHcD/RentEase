namespace PropertyLeasing.BusinessLogic;

public static class UserProfileRules
{
    public static string ResolveDisplayName(string? username, string? fullName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(username))
            return username.Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName.Trim();
        if (!string.IsNullOrWhiteSpace(email))
            return DefaultUsernameFromEmail(email);
        return "User";
    }

    public static string DefaultUsernameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        var local = at > 0 ? email[..at] : email;
        return SanitizeUsername(local);
    }

    public static string SanitizeUsername(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "user";
        var chars = value.Trim()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        while (sanitized.Contains("__", StringComparison.Ordinal))
            sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrEmpty(sanitized) ? "user" : sanitized;
    }

    public static bool IsValidUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username)
        && username.Length is >= 3 and <= 50
        && username.All(c => char.IsLetterOrDigit(c) || c == '_');
}
