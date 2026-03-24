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
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

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
            return JsonSerializer.Deserialize<AuthResponse>(json, _json);
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
            if (!response.IsSuccessStatusCode) return new();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<OccupancyReportItem>>(json, _json) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOccupancyReport failed");
            return new();
        }
    }

    // GET /api/reports/maintenance
    public async Task<MaintenanceReportItem?> GetMaintenanceReportAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/reports/maintenance");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MaintenanceReportItem>(json, _json);
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
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PaymentReportItem>(json, _json);
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
            if (!response.IsSuccessStatusCode) return new();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ApplicationReportItem>>(json, _json) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetApplicationsReport failed");
            return new();
        }
    }
}
