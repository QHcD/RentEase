using System.Text.Json;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Services;

// This service is used ONLY for the public lookup page
// It calls the Web API via HttpClient (no direct DB access)
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger     = logger;
    }

    // Called by the Public Lookup page — no auth needed
    public async Task<MaintenanceLookupResultViewModel?> LookupMaintenanceTicketAsync(
        string ticketNumber, string phone)
    {
        try
        {
            var url = $"/api/maintenance/lookup?ticket={Uri.EscapeDataString(ticketNumber)}&phone={Uri.EscapeDataString(phone)}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json    = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result  = JsonSerializer.Deserialize<MaintenanceLookupResultViewModel>(json, options);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling maintenance lookup API");
            return null;
        }
    }
}
