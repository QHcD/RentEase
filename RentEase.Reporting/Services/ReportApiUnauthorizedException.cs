namespace PropertyLeasing.Reporting.Services;

/// <summary>Raised when the report API rejects the JWT (expired, invalid, or missing role).</summary>
public sealed class ReportApiUnauthorizedException : Exception
{
    public ReportApiUnauthorizedException()
        : base("Report API returned 401 or 403.")
    {
    }
}
