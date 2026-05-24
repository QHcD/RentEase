namespace PropertyLeasing.BusinessLogic;

/// <summary>Role checks for property manager-only features (manage, add, edit).</summary>
public static class PropertyManagerAccessRules
{
    public const string ManagerRoleName = "PropertyManager";

    public static bool IsPropertyManager(string? role) =>
        string.Equals(role, ManagerRoleName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Actions on <see cref="PropertyManagerAccessRules"/> MVC controller restricted to managers.</summary>
    public static readonly string[] ManagerOnlyPropertyActions =
    {
        "Manage",
        "Add",
        "Edit",
        "Delete"
    };
}
