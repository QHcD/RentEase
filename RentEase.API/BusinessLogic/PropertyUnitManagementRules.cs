namespace PropertyLeasing.BusinessLogic;

/// <summary>Rules for unit lifecycle on existing properties.</summary>
public static class PropertyUnitManagementRules
{
    /// <summary>Individual units on an existing property cannot be deleted by managers.</summary>
    public const bool AllowManagerUnitDeletion = false;

    public static bool CanManagerDeleteUnit() => AllowManagerUnitDeletion;
}
