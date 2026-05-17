using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PropertyLeasing.Reporting.ViewModels;

namespace PropertyLeasing.Reporting.Services;

// This service is the ONLY way the Reporting App accesses data.
// It authenticates via JWT and calls the Web API endpoints.
// No direct database access at all.
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // Set JWT token on every request
    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static void EnsureAuthorized(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ReportApiUnauthorizedException();
    }

    private async Task<string?> ReadBodyForLogAsync(HttpResponseMessage response)
    {
        try
        {
            var s = await response.Content.ReadAsStringAsync();
            return s.Length > 512 ? s[..512] + "…" : s;
        }
        catch
        {
            return null;
        }
    }

    // Login and get JWT token from API
    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { email, password });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/api/auth/login", content);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AuthResponse>(json, ReportingJsonDefaults.SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return null;
        }
    }

    // GET /api/reports/occupancy
    public async Task<List<OccupancyReportItem>> GetOccupancyReportAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/reports/occupancy");
            EnsureAuthorized(response);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetOccupancyReport failed: {Status} — {Body}",
                    (int)response.StatusCode, await ReadBodyForLogAsync(response));
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<OccupancyReportItem>>(json, ReportingJsonDefaults.SerializerOptions) ?? [];
        }
        catch (ReportApiUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOccupancyReport failed");
            return [];
        }
    }

    // GET /api/reports/maintenance
    public async Task<MaintenanceReportItem?> GetMaintenanceReportAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/reports/maintenance");
            EnsureAuthorized(response);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetMaintenanceReport failed: {Status} — {Body}",
                    (int)response.StatusCode, await ReadBodyForLogAsync(response));
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MaintenanceReportItem>(json, ReportingJsonDefaults.SerializerOptions);
        }
        catch (ReportApiUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMaintenanceReport failed");
            return null;
        }
    }

    // GET /api/reports/payments
    public async Task<PaymentReportItem?> GetPaymentReportAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/reports/payments");
            EnsureAuthorized(response);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetPaymentReport failed: {Status} — {Body}",
                    (int)response.StatusCode, await ReadBodyForLogAsync(response));
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PaymentReportItem>(json, ReportingJsonDefaults.SerializerOptions);
        }
        catch (ReportApiUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPaymentReport failed");
            return null;
        }
    }

    // GET /api/reports/applications
    public async Task<List<ApplicationReportItem>> GetApplicationsReportAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/reports/applications");
            EnsureAuthorized(response);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetApplicationsReport failed: {Status} — {Body}",
                    (int)response.StatusCode, await ReadBodyForLogAsync(response));
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ApplicationReportItem>>(json, ReportingJsonDefaults.SerializerOptions) ?? [];
        }
        catch (ReportApiUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetApplicationsReport failed");
            return [];
        }
    }

    // GET /api/reports/leases
    public async Task<List<LeaseReportItem>> GetLeasesReportAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/reports/leases");
            EnsureAuthorized(response);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetLeasesReport failed: {Status} — {Body}",
                    (int)response.StatusCode, await ReadBodyForLogAsync(response));
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<LeaseReportItem>>(json, ReportingJsonDefaults.SerializerOptions) ?? [];
        }
        catch (ReportApiUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLeasesReport failed");
            return [];
        }
    }
}
