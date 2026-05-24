namespace PropertyLeasing.BusinessLogic;

/// <summary>Role checks for property manager-only features (manage, add, edit).</summary>
public static class PropertyManagerAccessRules
{
    public const string ManagerRoleName = "PropertyManager";

    public static bool IsPropertyManager(string? role) =>
        string.Equals(role, ManagerRoleName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Actions on the Properties MVC controller restricted to managers.</summary>
    public static readonly string[] ManagerOnlyPropertyActions =
    {
        "Manage",
        "Add",
        "Edit",
        "Delete"
    };

    /// <summary>Individual unit deletion is not exposed to managers (see <see cref="PropertyUnitManagementRules"/>).</summary>
    public static readonly string[] DisallowedManagerPropertyActions =
    {
        "DeleteUnit"
    };
}
