using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Models;
using PropertyLeasing.MVC.Services;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

public class HomeController : Controller
{
    private readonly PropertyLeasingDbContext _db;
    private readonly ApiService _apiService;

    public HomeController(PropertyLeasingDbContext db, ApiService apiService)
    {
        _db         = db;
        _apiService = apiService;
    }

    // GET / — public landing page
    public async Task<IActionResult> Index()
    {
        // Show stats on landing page
        ViewBag.TotalProperties  = await _db.Properties.CountAsync();
        ViewBag.AvailableUnits   = await _db.Units.CountAsync(u => u.AvailabilityStatus == "Available");
        ViewBag.TotalUnits       = await _db.Units.CountAsync();
        ViewBag.FeaturedProperties = await _db.Properties
            .Include(p => p.Units)
            .Take(3)
            .ToListAsync();
        return View();
    }

    // GET /Home/MaintenanceLookup — public page
    public IActionResult MaintenanceLookup()
    {
        return View(new PublicLookupViewModel());
    }

    // POST /Home/MaintenanceLookup — calls API via HttpClient
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MaintenanceLookup(PublicLookupViewModel model)
    {
        model.Searched = true;

        if (!ModelState.IsValid)
            return View(model);

        // Call the Web API endpoint via HttpClient (not direct DB access)
        var result = await _apiService.LookupMaintenanceTicketAsync(
            model.TicketNumber!, model.Phone!);

        if (result == null)
        {
            model.ErrorMessage = "No maintenance request found with the provided ticket number and phone. Please check your details and try again.";
        }
        else
        {
            model.Result = result;
        }

        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
