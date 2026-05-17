using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyLeasing.Reporting.Services;
using PropertyLeasing.Reporting.ViewModels;
using System.Security.Claims;

namespace PropertyLeasing.Reporting.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApiClient _api;

    public ReportsController(ApiClient api)
    {
        _api = api;
    }

    // Inject JWT token from session before each API call
    private void SetApiToken()
    {
        string? token = null;

        // Try session first
        try { token = HttpContext.Session.GetString("JwtToken"); } catch { }

        // Fallback to claims
        if (string.IsNullOrEmpty(token))
            token = User.FindFirstValue("JwtToken");

        if (!string.IsNullOrEmpty(token))
            _api.SetToken(token);
    }

    // GET /Reports/Occupancy
    public async Task<IActionResult> Occupancy()
    {
        SetApiToken();
        var data = await _api.GetOccupancyReportAsync();
        return View(data);
    }

    // GET /Reports/Maintenance
    public async Task<IActionResult> Maintenance()
    {
        SetApiToken();
        var data = await _api.GetMaintenanceReportAsync();
        return View(data);
    }

    // GET /Reports/Payments
    public async Task<IActionResult> Payments()
    {
        SetApiToken();
        var data = await _api.GetPaymentReportAsync();
        return View(data);
    }

    // GET /Reports/Applications
    public async Task<IActionResult> Applications(string? status, string? leaseStatus, string? tab)
    {
        SetApiToken();
        var applications = await _api.GetApplicationsReportAsync();
        var leases = await _api.GetLeasesReportAsync();

        if (!string.IsNullOrWhiteSpace(status))
            applications = applications.Where(a => a.Status == status).ToList();

        if (!string.IsNullOrWhiteSpace(leaseStatus))
            leases = leases.Where(l => l.Status == leaseStatus).ToList();

        ViewBag.Status = status;
        ViewBag.LeaseStatus = leaseStatus;
        ViewBag.ActiveTab = string.Equals(tab, "leases", StringComparison.OrdinalIgnoreCase)
            ? "leases"
            : "applications";

        return View(new ApplicationsLeasesReportViewModel
        {
            Applications = applications,
            Leases = leases
        });
    }
}
