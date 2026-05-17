using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

        try { token = HttpContext.Session.GetString("JwtToken"); } catch { }

        if (string.IsNullOrEmpty(token))
            token = User.FindFirstValue("JwtToken");

        if (!string.IsNullOrEmpty(token))
            _api.SetToken(token);
    }

    /// <summary>Clears session/cookie and sends user to login when the API JWT is invalid or expired.</summary>
    private async Task<IActionResult> WithReportApi(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ReportApiUnauthorizedException)
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SessionError"] =
                "The reporting API rejected your session (JWT expired or invalid). Please sign in again.";
            return RedirectToAction("Login", "Account");
        }
    }

    // GET /Reports/Occupancy
    public Task<IActionResult> Occupancy() =>
        WithReportApi(async () =>
        {
            SetApiToken();
            var data = await _api.GetOccupancyReportAsync();
            return View(data);
        });

    // GET /Reports/Maintenance
    public Task<IActionResult> Maintenance() =>
        WithReportApi(async () =>
        {
            SetApiToken();
            var data = await _api.GetMaintenanceReportAsync();
            return View(data);
        });

    // GET /Reports/Payments
    public Task<IActionResult> Payments() =>
        WithReportApi(async () =>
        {
            SetApiToken();
            var data = await _api.GetPaymentReportAsync();
            return View(data);
        });

    // GET /Reports/Applications
    public Task<IActionResult> Applications(string? status, string? leaseStatus, string? tab) =>
        WithReportApi(async () =>
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
        });
}
