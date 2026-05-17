using System.Text.Json;

namespace PropertyLeasing.Reporting.Services;

/// <summary>Shared JSON settings for API responses (camelCase from ASP.NET Core).</summary>
public static class ReportingJsonDefaults
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
