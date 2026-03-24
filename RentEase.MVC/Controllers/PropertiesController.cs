using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Models;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

public class PropertiesController : Controller
{
    private readonly PropertyLeasingDbContext _db;

    public PropertiesController(PropertyLeasingDbContext db)
    {
        _db = db;
    }

    // GET /Properties — all properties (public)
    public async Task<IActionResult> Index(string? search, string? type)
    {
        var query = _db.Properties.Include(p => p.Units).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Address.Contains(search) || p.City!.Contains(search));

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.PropertyType == type);

        var properties = await query
            .Select(p => new PropertyListViewModel
            {
                PropertyId      = p.PropertyId,
                Name            = p.Name,
                Description     = p.Description,
                Address         = p.Address,
                City            = p.City,
                PropertyType    = p.PropertyType,
                ImgPath         = p.ImgPath,
                TotalUnits      = p.Units.Count,
                AvailableUnits  = p.Units.Count(u => u.AvailabilityStatus == "Available")
            })
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Type   = type;
        return View(properties);
    }

    // GET /Properties/Units/{propertyId}
    public async Task<IActionResult> Units(int propertyId, string? unitType, decimal? maxRent)
    {
        var property = await _db.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        var query = _db.Units
            .Include(u => u.Property)
            .Include(u => u.Feedbacks)
            .Where(u => u.PropertyId == propertyId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(unitType))
            query = query.Where(u => u.UnitType == unitType);

        if (maxRent.HasValue)
            query = query.Where(u => u.MonthlyRent <= maxRent);

        var units = await query
            .Select(u => new UnitListViewModel
            {
                UnitId             = u.UnitId,
                UnitNumber         = u.UnitNumber,
                UnitType           = u.UnitType,
                Sizesqm            = u.Sizesqm,
                MonthlyRent        = u.MonthlyRent,
                Amenities          = u.Amenities,
                AvailabilityStatus = u.AvailabilityStatus,
                ImgPath            = u.ImgPath,
                PropertyName       = u.Property.Name,
                PropertyAddress    = u.Property.Address,
                PropertyId         = u.PropertyId,
                AverageRating      = u.Feedbacks.Any() ? u.Feedbacks.Average(f => (double)(f.Rating ?? 0)) : 0,
                FeedbackCount      = u.Feedbacks.Count(f => f.IsVisible)
            })
            .ToListAsync();

        ViewBag.PropertyName = property.Name;
        ViewBag.PropertyId   = propertyId;
        ViewBag.UnitType     = unitType;
        ViewBag.MaxRent      = maxRent;
        return View(units);
    }

    // GET /Properties/UnitDetails/{id}
    public async Task<IActionResult> UnitDetails(int id)
    {
        var unit = await _db.Units
            .Include(u => u.Property)
            .Include(u => u.Feedbacks.Where(f => f.IsVisible))
                .ThenInclude(f => f.User)
            .FirstOrDefaultAsync(u => u.UnitId == id);

        if (unit == null) return NotFound();
        return View(unit);
    }

    // ── Manager only: Manage Properties ────────────────

    // GET /Properties/Manage
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> Manage()
    {
        var properties = await _db.Properties
            .Include(p => p.Units)
            .ToListAsync();
        return View(properties);
    }

    // GET /Properties/Create
    [Authorize(Roles = "PropertyManager")]
    public IActionResult Create() => View();

    // POST /Properties/Create
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Property model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Properties.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Property created successfully.";
        return RedirectToAction("Manage");
    }

    // GET /Properties/Edit/{id}
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> Edit(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property == null) return NotFound();
        return View(property);
    }

    // POST /Properties/Edit/{id}
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Property model)
    {
        if (id != model.PropertyId) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        _db.Properties.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Property updated successfully.";
        return RedirectToAction("Manage");
    }

    // POST /Properties/Delete/{id}
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property == null) return NotFound();
        _db.Properties.Remove(property);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Property deleted.";
        return RedirectToAction("Manage");
    }
}
