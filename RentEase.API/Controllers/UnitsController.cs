using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.DTOs;
using PropertyLeasing.API.Models;

namespace PropertyLeasing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnitsController : ControllerBase
{
    private readonly PropertyLeasingDbContext _db;

    public UnitsController(PropertyLeasingDbContext db)
    {
        _db = db;
    }

    // GET api/units — public, all available units
    [HttpGet]
    public async Task<IActionResult> GetAvailableUnits()
    {
        var units = await _db.Units
            .Include(u => u.Property)
            .Where(u => u.AvailabilityStatus == "Available")
            .Select(u => new UnitDto
            {
                UnitId             = u.UnitId,
                UnitNumber         = u.UnitNumber,
                UnitType           = u.UnitType,
                Sizesqm            = u.Sizesqm,
                MonthlyRent        = u.MonthlyRent,
                Amenities          = u.Amenities,
                AvailabilityStatus = u.AvailabilityStatus,
                PropertyName       = u.Property.Name,
                PropertyAddress    = u.Property.Address
            })
            .ToListAsync();

        return Ok(units);
    }

    // GET api/units/{id} — public, single unit details
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUnit(int id)
    {
        var unit = await _db.Units
            .Include(u => u.Property)
            .Where(u => u.UnitId == id)
            .Select(u => new UnitDto
            {
                UnitId             = u.UnitId,
                UnitNumber         = u.UnitNumber,
                UnitType           = u.UnitType,
                Sizesqm            = u.Sizesqm,
                MonthlyRent        = u.MonthlyRent,
                Amenities          = u.Amenities,
                AvailabilityStatus = u.AvailabilityStatus,
                PropertyName       = u.Property.Name,
                PropertyAddress    = u.Property.Address
            })
            .FirstOrDefaultAsync();

        if (unit == null) return NotFound(new { message = "Unit not found." });
        return Ok(unit);
    }
}
