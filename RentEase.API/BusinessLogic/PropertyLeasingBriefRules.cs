namespace PropertyLeasing.BusinessLogic;

/// <summary>IT8118 Brief B scope — core functional areas and out-of-scope features.</summary>
public static class PropertyLeasingBriefRules
{
    /// <summary>Brief B required functional areas (property leasing &amp; maintenance platform).</summary>
    public static readonly string[] BriefBCoreFunctionalAreas =
    {
        "PropertyAndUnitManagement",
        "LeaseLifecycle",
        "MaintenanceManagement",
        "StaffManagement",
        "Payments",
        "Notifications"
    };

    /// <summary>Tenant unit reviews/ratings are not part of Brief B.</summary>
    public const bool TenantUnitReviewsRequired = false;

    public static readonly string[] OutOfScopeFeatures =
    {
        "TenantUnitReviews"
    };

    public static bool IsFeatureInBriefBScope(string featureKey) =>
        !OutOfScopeFeatures.Contains(featureKey, StringComparer.OrdinalIgnoreCase);
}
