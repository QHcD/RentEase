using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.DTOs;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;

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
            .Include(u => u.UnitAmenities)
            .ThenInclude(ua => ua.Amenity)
            .Where(u => u.AvailabilityStatus == "Available")
            .ToListAsync();

        return Ok(units.Select(u => new UnitDto
        {
            UnitId             = u.UnitId,
            UnitNumber         = u.UnitNumber,
            UnitType           = u.UnitType,
            Sizesqm            = u.Sizesqm,
            MonthlyRent        = u.MonthlyRent,
            Amenities          = AmenityLinkService.JoinDisplayNames(AmenityLinkService.GetUnitAmenityNames(u)),
            AvailabilityStatus = u.AvailabilityStatus,
            PropertyName       = u.Property.Name,
            PropertyAddress    = u.Property.Address
        }));
    }

    // GET api/units/{id} — public, single unit details
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUnit(int id)
    {
        var unitEntity = await _db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitAmenities)
            .ThenInclude(ua => ua.Amenity)
            .FirstOrDefaultAsync(u => u.UnitId == id);

        if (unitEntity == null) return NotFound(new { message = "Unit not found." });

        var unit = new UnitDto
        {
            UnitId             = unitEntity.UnitId,
            UnitNumber         = unitEntity.UnitNumber,
            UnitType           = unitEntity.UnitType,
            Sizesqm            = unitEntity.Sizesqm,
            MonthlyRent        = unitEntity.MonthlyRent,
            Amenities          = AmenityLinkService.JoinDisplayNames(AmenityLinkService.GetUnitAmenityNames(unitEntity)),
            AvailabilityStatus = unitEntity.AvailabilityStatus,
            PropertyName       = unitEntity.Property.Name,
            PropertyAddress    = unitEntity.Property.Address
        };

        return Ok(unit);
    }
}
