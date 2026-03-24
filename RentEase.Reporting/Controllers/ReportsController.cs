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

    // GET /Reports — main dashboard with all reports
    public async Task<IActionResult> Index()
    {
        SetApiToken();

        // Call all API endpoints in parallel for speed
        var occupancyTask    = _api.GetOccupancyReportAsync();
        var maintenanceTask  = _api.GetMaintenanceReportAsync();
        var paymentTask      = _api.GetPaymentReportAsync();
        var applicationsTask = _api.GetApplicationsReportAsync();

        await Task.WhenAll(occupancyTask, maintenanceTask, paymentTask, applicationsTask);

        var model = new ReportDashboardViewModel
        {
            OccupancyReport   = await occupancyTask,
            MaintenanceReport = await maintenanceTask,
            PaymentReport     = await paymentTask,
            Applications      = await applicationsTask
        };

        return View(model);
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
    public async Task<IActionResult> Applications(string? status)
    {
        SetApiToken();
        var data = await _api.GetApplicationsReportAsync();

        if (!string.IsNullOrWhiteSpace(status))
            data = data.Where(a => a.Status == status).ToList();

        ViewBag.Status = status;
        return View(data);
    }
}
