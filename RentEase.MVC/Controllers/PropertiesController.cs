using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;
using PropertyLeasing.MVC.Helpers;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

public class PropertiesController : Controller
{
    private readonly PropertyLeasingDbContext _db;
    private readonly IWebHostEnvironment _env;

    public PropertiesController(PropertyLeasingDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    // GET /Properties — all properties (public)
    public async Task<IActionResult> Index(string? search, string? type)
    {
        var query = _db.Properties.Include(p => p.Units).Include(p => p.PropertyImages).AsQueryable();

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
                AvailableUnits  = p.Units.Count(u => u.AvailabilityStatus == "Available"),
                ImagePaths      = p.PropertyImages.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).ToList()
            })
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Type   = type;
        if (User.IsInRole("PropertyManager"))
            ViewBag.BlockingDeletePropertyIds = await PropertyUnitDeletionHelper.GetPropertyIdsWithBlockingLeasesAsync(_db);

        return View(properties);
    }

    // GET /Properties/Units?propertyId=…&avail=…&types=…&maxRent=…
    // avail: comma-separated (Available,Occupied,UnderMaintenance) or all | types: comma-separated unit types
    public async Task<IActionResult> Units(int propertyId, decimal? maxRent)
    {
        var property = await _db.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        var (availShowAll, availStatuses) = PropertyUnitsFilterHelper.ParseAvailability(Request.Query);
        var unitTypesFilter = PropertyUnitsFilterHelper.ParseUnitTypes(Request.Query);

        var query = _db.Units
            .Include(u => u.Property)
            .Include(u => u.Feedbacks)
            .Include(u => u.UnitImages)
            .Where(u => u.PropertyId == propertyId)
            .AsQueryable();

        if (!availShowAll && availStatuses.Count > 0)
            query = query.Where(u => availStatuses.Contains(u.AvailabilityStatus));

        if (unitTypesFilter.Count > 0)
            query = query.Where(u => unitTypesFilter.Contains(u.UnitType));

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
                FeedbackCount      = u.Feedbacks.Count(f => f.IsVisible),
                ImagePaths         = u.UnitImages.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).ToList()
            })
            .ToListAsync();

        ViewBag.PropertyName = property.Name;
        ViewBag.PropertyId   = propertyId;
        ViewBag.MaxRent      = maxRent;
        ViewBag.AvailShowAll = availShowAll;
        ViewBag.AvailSelection = availStatuses;
        ViewBag.UnitTypesSelection = unitTypesFilter;
        if (User.IsInRole("PropertyManager"))
            ViewBag.BlockingLeaseUnitIds = await PropertyUnitDeletionHelper.GetBlockingUnitIdsForPropertyAsync(_db, propertyId);

        return View(units);
    }

    // GET /Properties/UnitDetails/{id}
    public async Task<IActionResult> UnitDetails(int id)
    {
        var unit = await _db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitImages.OrderBy(i => i.SortOrder))
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
        ViewBag.BlockingDeletePropertyIds = await PropertyUnitDeletionHelper.GetPropertyIdsWithBlockingLeasesAsync(_db);
        return View(properties);
    }

    // GET /Properties/Create
    [Authorize(Roles = "PropertyManager")]
    public IActionResult Create()
    {
        var vm = new CreatePropertyViewModel
        {
            NumberOfFloors = 1,
            FloorRows      = new List<FloorUnitRowInput> { new() }
        };
        return View(vm);
    }

    // POST /Properties/Create
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePropertyViewModel model)
    {
        if (model.FloorRows == null || model.FloorRows.Count != model.NumberOfFloors)
            ModelState.AddModelError(string.Empty,
                "Floor rows must match the number of floors. Adjust the floor count or refresh the page.");

        if (model.CustomAmenities is { Count: > 0 } &&
            model.CustomAmenities.Count > PropertyAmenitySelection.MaxCustomAmenityItems)
            ModelState.AddModelError(nameof(model.CustomAmenities),
                $"At most {PropertyAmenitySelection.MaxCustomAmenityItems} custom amenities are allowed.");

        if (model.CustomAmenities is { Count: > 0 })
        {
            if (model.CustomAmenities.Any(c => (c?.Trim() ?? string.Empty).Length >
                                                PropertyAmenitySelection.MaxCustomAmenityItemLength))
                ModelState.AddModelError(nameof(model.CustomAmenities),
                    $"Each custom amenity must be at most {PropertyAmenitySelection.MaxCustomAmenityItemLength} characters.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var mergedAmenities = PropertyAmenitySelection.Merge(
            model.SelectedFixedAmenities,
            model.CustomAmenities,
            PropertyAmenityOptions.All).ToList();

        var amenitiesJoined = PropertyAmenitySelection.JoinForUnit(mergedAmenities);
        var amenitiesLengthError = PropertyAmenitySelection.ValidateJoinedLength(amenitiesJoined);
        if (amenitiesLengthError != null)
            ModelState.AddModelError(nameof(model.CustomAmenities), amenitiesLengthError);

        if (!ModelState.IsValid)
            return View(model);

        IReadOnlyList<string> unitNumbers;
        try
        {
            var prefix = model.UnitNumberPrefix;
            var floors = model.FloorRows!
                .Select(r => ((string?)prefix, r.UnitsOnFloor))
                .ToList();
            unitNumbers = PropertyCreateUnitNaming.BuildUnitNumbers(floors);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, "Invalid floor layout. Each floor needs 1–99 units.");
            return View(model);
        }

        var property = new Property
        {
            Name             = model.Name,
            Description      = model.Description,
            Address          = model.Address,
            City             = model.City,
            PropertyType     = model.PropertyType,
            GracePeriodDays  = 5,
            LateFeePercent   = 5
        };

        foreach (var number in unitNumbers)
        {
            property.Units.Add(new Unit
            {
                UnitNumber           = number,
                Amenities            = amenitiesJoined,
                AvailabilityStatus   = "Available"
            });
        }

        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Property created successfully with {unitNumbers.Count} unit(s).";
        return RedirectToAction("Manage");
    }

    // GET /Properties/Edit/{id}
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> Edit(int id)
    {
        var property = await _db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.PropertyId == id);
        if (property == null) return NotFound();

        var amenitySource = await _db.Units
            .Where(u => u.PropertyId == id)
            .OrderBy(u => u.UnitId)
            .Select(u => u.Amenities)
            .FirstOrDefaultAsync();

        var (fixedSel, customs) = PropertyAmenitySelection.SplitFromStoredString(amenitySource, PropertyAmenityOptions.All);

        var vm = new EditPropertyViewModel
        {
            PropertyId      = property.PropertyId,
            Name            = property.Name,
            Description     = property.Description,
            Address         = property.Address,
            City            = property.City,
            PropertyType    = property.PropertyType,
            ImgPath         = property.ImgPath,
            GracePeriodDays = property.GracePeriodDays,
            LateFeePercent  = property.LateFeePercent,
            SelectedFixedAmenities = fixedSel,
            CustomAmenities        = customs
        };

        ViewBag.PropertyImages = await _db.PropertyImages
            .Where(i => i.PropertyId == id)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        return View(vm);
    }

    // POST /Properties/Edit/{id}
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditPropertyViewModel model)
    {
        if (id != model.PropertyId) return BadRequest();

        if (model.CustomAmenities is { Count: > 0 } &&
            model.CustomAmenities.Count > PropertyAmenitySelection.MaxCustomAmenityItems)
            ModelState.AddModelError(nameof(model.CustomAmenities),
                $"At most {PropertyAmenitySelection.MaxCustomAmenityItems} custom amenities are allowed.");

        if (model.CustomAmenities is { Count: > 0 })
        {
            if (model.CustomAmenities.Any(c => (c?.Trim() ?? string.Empty).Length >
                                                PropertyAmenitySelection.MaxCustomAmenityItemLength))
                ModelState.AddModelError(nameof(model.CustomAmenities),
                    $"Each custom amenity must be at most {PropertyAmenitySelection.MaxCustomAmenityItemLength} characters.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var mergedAmenities = PropertyAmenitySelection.Merge(
            model.SelectedFixedAmenities,
            model.CustomAmenities,
            PropertyAmenityOptions.All).ToList();

        var amenitiesJoined = PropertyAmenitySelection.JoinForUnit(mergedAmenities);
        var amenitiesLengthError = PropertyAmenitySelection.ValidateJoinedLength(amenitiesJoined);
        if (amenitiesLengthError != null)
            ModelState.AddModelError(nameof(model.CustomAmenities), amenitiesLengthError);

        if (!ModelState.IsValid)
            return View(model);

        var entity = await _db.Properties.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name             = model.Name;
        entity.Description      = model.Description;
        entity.Address          = model.Address;
        entity.City             = model.City;
        entity.PropertyType     = model.PropertyType;
        entity.ImgPath          = model.ImgPath;
        entity.GracePeriodDays  = model.GracePeriodDays;
        entity.LateFeePercent   = model.LateFeePercent;

        await _db.SaveChangesAsync();

        await _db.Units.Where(u => u.PropertyId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Amenities, amenitiesJoined));

        TempData["Success"] = "Property updated successfully.";
        return RedirectToAction(nameof(Manage));
    }

    // POST /Properties/Delete/{id}
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.PropertyId == id);
        if (property == null) return NotFound();

        var unitIds = await _db.Units.Where(u => u.PropertyId == id).Select(u => u.UnitId).ToListAsync();

        try
        {
            var (ok, error) = await PropertyUnitDeletionHelper.TryCascadeDeleteUnitsAsync(_db, unitIds);
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Manage));
            }

            var propRow = await _db.Properties.FindAsync(id);
            if (propRow != null)
            {
                _db.Properties.Remove(propRow);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Property deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Unable to delete this property because related data could not be removed. Try again or contact support.";
        }

        return RedirectToAction(nameof(Manage));
    }

    // POST /Properties/DeleteUnit — manager only; removes one unit if no blocking lease.
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnit(int unitId, int propertyId)
    {
        var unit = await _db.Units.FirstOrDefaultAsync(u => u.UnitId == unitId && u.PropertyId == propertyId);
        if (unit == null) return NotFound();

        try
        {
            var (ok, error) = await PropertyUnitDeletionHelper.TryCascadeDeleteUnitsAsync(_db, new List<int> { unitId });
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Units), new { propertyId });
            }

            TempData["Success"] = $"Unit {unit.UnitNumber} was deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Unable to delete this unit because related data could not be removed. Try again or contact support.";
        }

        return RedirectToAction(nameof(Units), new { propertyId });
    }

    // ── Image Management ─────────────────────────────────────────────────────

    private static readonly string[] AllowedImgExt = { ".jpg", ".jpeg", ".png", ".webp" };

    // POST /Properties/UploadPropertyImages
    [Authorize(Roles = "PropertyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPropertyImages(int propertyId, List<IFormFile> files)
    {
        var property = await _db.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        var dir = Path.Combine(_env.WebRootPath, "uploads", "properties", propertyId.ToString());
        Directory.CreateDirectory(dir);
        int order = await _db.PropertyImages.Where(i => i.PropertyId == propertyId).CountAsync();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImgExt.Contains(ext)) continue;
            var name = $"{Guid.NewGuid()}{ext}";
            await using var fs = System.IO.File.Create(Path.Combine(dir, name));
            await file.CopyToAsync(fs);
            _db.PropertyImages.Add(new PropertyImage
            {
                PropertyId = propertyId,
                ImagePath  = $"/uploads/properties/{propertyId}/{name}",
                SortOrder  = order++
            });
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Images uploaded.";
        return RedirectToAction("Edit", new { id = propertyId });
    }

    // POST /Properties/DeletePropertyImage
    [Authorize(Roles = "PropertyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePropertyImage(int imageId, int propertyId)
    {
        var img = await _db.PropertyImages.FindAsync(imageId);
        if (img != null)
        {
            var path = Path.Combine(_env.WebRootPath, img.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            _db.PropertyImages.Remove(img);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Edit", new { id = propertyId });
    }

    // POST /Properties/UploadUnitImages
    [Authorize(Roles = "PropertyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadUnitImages(int unitId, List<IFormFile> files)
    {
        var unit = await _db.Units.FindAsync(unitId);
        if (unit == null) return NotFound();

        var dir = Path.Combine(_env.WebRootPath, "uploads", "units", unitId.ToString());
        Directory.CreateDirectory(dir);
        int order = await _db.UnitImages.Where(i => i.UnitId == unitId).CountAsync();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImgExt.Contains(ext)) continue;
            var name = $"{Guid.NewGuid()}{ext}";
            await using var fs = System.IO.File.Create(Path.Combine(dir, name));
            await file.CopyToAsync(fs);
            _db.UnitImages.Add(new UnitImage
            {
                UnitId    = unitId,
                ImagePath = $"/uploads/units/{unitId}/{name}",
                SortOrder = order++
            });
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Images uploaded.";
        return RedirectToAction("UnitDetails", new { id = unitId });
    }

    // POST /Properties/DeleteUnitImage
    [Authorize(Roles = "PropertyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnitImage(int imageId, int unitId)
    {
        var img = await _db.UnitImages.FindAsync(imageId);
        if (img != null)
        {
            var path = Path.Combine(_env.WebRootPath, img.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            _db.UnitImages.Remove(img);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("UnitDetails", new { id = unitId });
    }
}
