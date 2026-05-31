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
            .Include(u => u.UnitAmenities)
            .ThenInclude(ua => ua.Amenity)
            .ToListAsync();

        var unitModels = units.Select(u => new UnitListViewModel
            {
                UnitId             = u.UnitId,
                UnitNumber         = u.UnitNumber,
                UnitType           = u.UnitType,
                Sizesqm            = u.Sizesqm,
                MonthlyRent        = u.MonthlyRent,
                Amenities          = AmenityLinkService.JoinDisplayNames(AmenityLinkService.GetUnitAmenityNames(u)),
                AvailabilityStatus = u.AvailabilityStatus,
                ImgPath            = u.ImgPath,
                PropertyName       = u.Property.Name,
                PropertyAddress    = u.Property.Address,
                PropertyId         = u.PropertyId,
                ImagePaths         = u.UnitImages.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).ToList()
            })
            .ToList();

        ViewBag.PropertyName = property.Name;
        ViewBag.PropertyId   = propertyId;
        ViewBag.MaxRent      = maxRent;
        ViewBag.AvailShowAll = availShowAll;
        ViewBag.AvailSelection = availStatuses;
        ViewBag.UnitTypesSelection = unitTypesFilter;
        ViewBag.PropertyImagePaths = await _db.PropertyImages
            .Where(i => i.PropertyId == propertyId)
            .OrderBy(i => i.SortOrder)
            .Select(i => i.ImagePath)
            .ToListAsync();

        return View(unitModels);
    }

    // GET /Properties/UnitDetails/{id}
    public async Task<IActionResult> UnitDetails(int id)
    {
        var unit = await _db.Units
            .Include(u => u.Property)
                .ThenInclude(p => p.PropertyAmenities)
                .ThenInclude(pa => pa.Amenity)
            .Include(u => u.UnitAmenities)
                .ThenInclude(ua => ua.Amenity)
            .Include(u => u.UnitImages.OrderBy(i => i.SortOrder))
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

    // GET /Properties/Add
    [Authorize(Roles = "PropertyManager")]
    public IActionResult Add()
    {
        var defaultPlans = PropertyAreaAllocationRules.BuildDefaultFloorPlans(500m, new[] { 1 });
        var vm = new CreatePropertyViewModel
        {
            NumberOfFloors = 1,
            TotalSizeSqm   = 500m,
            FloorRows      = defaultPlans.Select(p => new FloorUnitRowInput
            {
                UnitsOnFloor = p.UnitAreasSqm.Count,
                UnitAreasSqm = p.UnitAreasSqm.ToList(),
                UnitMonthlyRents = p.UnitAreasSqm.Select(_ => (decimal?)null).ToList(),
                UnitCustomAmenities = p.UnitAreasSqm
                    .Select(_ => new List<string>())
                    .ToList()
            }).ToList()
        };
        return View(vm);
    }

    // POST /Properties/Add
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(CreatePropertyViewModel model)
    {
        if (model.FloorRows == null || model.FloorRows.Count != model.NumberOfFloors)
            ModelState.AddModelError(string.Empty,
                "Floor rows must match the number of floors. Adjust the floor count or refresh the page.");

        foreach (var sizeError in PropertyAreaAllocationRules.ValidatePropertySize(model.TotalSizeSqm))
            ModelState.AddModelError(nameof(model.TotalSizeSqm), sizeError);

        IReadOnlyList<string> areaWarnings = Array.Empty<string>();
        if (model.FloorRows != null && model.FloorRows.Count == model.NumberOfFloors)
        {
            var floorInputs = model.FloorRows
                .Select(r => (r.UnitsOnFloor, (IReadOnlyList<decimal>)(r.UnitAreasSqm ?? new List<decimal>())))
                .ToList();

            var unitsPerFloor = model.FloorRows.Select(r => r.UnitsOnFloor).ToList();
            foreach (var layoutError in PropertyAreaAllocationRules.ValidatePropertySize(
                         model.TotalSizeSqm, model.NumberOfFloors, unitsPerFloor))
                ModelState.AddModelError(nameof(model.TotalSizeSqm), layoutError);

            var (areaErrors, warnings) = PropertyAreaAllocationRules.ValidatePropertyAreaAllocationDetailed(
                model.TotalSizeSqm, floorInputs);
            areaWarnings = warnings;
            foreach (var areaError in areaErrors)
                ModelState.AddModelError(string.Empty, areaError);
        }

        ViewBag.AreaWarnings = areaWarnings;

        foreach (var propertyAmenityError in PropertyAmenitySelection.ValidateCustomAmenityList(model.CustomAmenities))
            ModelState.AddModelError(nameof(model.CustomAmenities), propertyAmenityError);

        var mergedPropertyAmenities = PropertyAmenitySelection.Merge(
            model.SelectedFixedAmenities,
            model.CustomAmenities,
            PropertyAmenityOptions.All).ToList();

        if (model.FloorRows != null)
        {
            for (var floorIdx = 0; floorIdx < model.FloorRows.Count; floorIdx++)
            {
                var floor = model.FloorRows[floorIdx];
                for (var unitIdx = 0; unitIdx < floor.UnitsOnFloor; unitIdx++)
                {
                    var customs = floor.UnitCustomAmenities != null && unitIdx < floor.UnitCustomAmenities.Count
                        ? floor.UnitCustomAmenities[unitIdx]
                        : new List<string>();

                    foreach (var unitAmenityError in PropertyAmenitySelection.ValidateCustomAmenityList(customs))
                    {
                        ModelState.AddModelError(string.Empty,
                            $"Floor {floorIdx + 1}, unit {unitIdx + 1}: {unitAmenityError}");
                    }

                    foreach (var duplicateError in PropertyAmenitySelection.ValidateUnitAmenitiesAgainstProperty(
                                 customs, mergedPropertyAmenities))
                    {
                        ModelState.AddModelError(string.Empty,
                            $"Floor {floorIdx + 1}, unit {unitIdx + 1}: {duplicateError}");
                    }
                }
            }
        }

        var propertyAmenitiesJoined = PropertyAmenitySelection.JoinForUnit(mergedPropertyAmenities);
        var propertyAmenitiesLengthError = PropertyAmenitySelection.ValidateJoinedLength(propertyAmenitiesJoined);
        if (propertyAmenitiesLengthError != null)
            ModelState.AddModelError(nameof(model.CustomAmenities), propertyAmenitiesLengthError);

        IReadOnlyList<PropertyMonthlyRentRules.FloorMonthlyRentInput> floorRentInputs = Array.Empty<PropertyMonthlyRentRules.FloorMonthlyRentInput>();
        if (model.FloorRows != null && model.FloorRows.Count == model.NumberOfFloors)
        {
            floorRentInputs = model.FloorRows
                .Select(r => new PropertyMonthlyRentRules.FloorMonthlyRentInput(
                    r.UnitsOnFloor,
                    r.FloorMonthlyRent,
                    r.UnitMonthlyRents))
                .ToList();

            foreach (var rentError in PropertyMonthlyRentRules.ValidateMonthlyRentLayout(
                         model.DefaultMonthlyRent, floorRentInputs))
            {
                ModelState.AddModelError(nameof(model.DefaultMonthlyRent), rentError);
            }
        }

        if (!ModelState.IsValid || areaWarnings.Count > 0)
            return View(model);

        IReadOnlyList<string> unitNumbers;
        IReadOnlyList<double> unitAreasSqm;
        IReadOnlyList<IReadOnlyList<string>> unitCustomAmenitiesFlat;
        IReadOnlyList<decimal> unitMonthlyRents;
        IReadOnlyList<string?> unitTypesFlat;
        try
        {
            var prefix = model.UnitNumberPrefix;
            var floors = model.FloorRows!
                .Select(r => ((string?)prefix, r.UnitsOnFloor))
                .ToList();
            unitNumbers = PropertyCreateUnitNaming.BuildUnitNumbers(floors);

            var areaFloors = model.FloorRows!
                .Select(r => (r.UnitsOnFloor, (IReadOnlyList<decimal>)(r.UnitAreasSqm ?? new List<decimal>())))
                .ToList();
            unitAreasSqm = PropertyAreaAllocationRules.FlattenUnitAreasSqm(model.TotalSizeSqm, areaFloors);

            unitCustomAmenitiesFlat = model.FloorRows!
                .SelectMany(r =>
                {
                    var count = r.UnitsOnFloor;
                    var lists = r.UnitCustomAmenities ?? new List<List<string>>();
                    return Enumerable.Range(0, count).Select(u =>
                        (IReadOnlyList<string>)(u < lists.Count ? lists[u] ?? new List<string>() : new List<string>()));
                })
                .ToList();

            unitTypesFlat = model.FloorRows!
                .SelectMany(r =>
                {
                    var count = r.UnitsOnFloor;
                    var list = r.UnitTypes ?? new List<string?>();
                    return Enumerable.Range(0, count).Select(u =>
                        u < list.Count ? list[u] : null);
                })
                .ToList();

            unitMonthlyRents = PropertyMonthlyRentRules.ResolveAllUnitRents(
                model.DefaultMonthlyRent, floorRentInputs);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, "Invalid floor layout. Each floor needs 1–99 units.");
            return View(model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        var property = new Property
        {
            Name             = model.Name,
            Description      = model.Description,
            Address          = model.Address,
            City             = model.City,
            PropertyType     = model.PropertyType,
            TotalSizeSqm     = (double)model.TotalSizeSqm,
            GracePeriodDays  = 5,
            LateFeePercent   = 5
        };

        for (var i = 0; i < unitNumbers.Count; i++)
        {
            var unitCustoms = i < unitCustomAmenitiesFlat.Count
                ? unitCustomAmenitiesFlat[i]
                : Array.Empty<string>();

            property.Units.Add(new Unit
            {
                UnitNumber           = unitNumbers[i],
                Sizesqm              = unitAreasSqm[i],
                MonthlyRent          = unitMonthlyRents[i],
                UnitType             = i < unitTypesFlat.Count ? unitTypesFlat[i] : null,
                AvailabilityStatus   = "Available"
            });
        }

        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        await AmenityLinkService.SyncPropertyAmenitiesAsync(_db, property.PropertyId, mergedPropertyAmenities);

        var unitIndex = 0;
        foreach (var unit in property.Units)
        {
            var unitCustoms = unitIndex < unitCustomAmenitiesFlat.Count
                ? unitCustomAmenitiesFlat[unitIndex]
                : Array.Empty<string>();
            await AmenityLinkService.SyncUnitAmenitiesAsync(
                _db,
                unit.UnitId,
                PropertyAmenitySelection.Merge(null, unitCustoms, Array.Empty<string>()));
            unitIndex++;
        }

        // Save optional per-unit images uploaded from the Add form.
        // Inputs are named unitImages_F{floor}_U{unit}; units were created in floor-major order.
        var createdUnits = property.Units.ToList();
        int flatUnitIndex = 0;
        for (var f = 0; f < model.FloorRows!.Count; f++)
        {
            var unitsOnFloor = model.FloorRows[f].UnitsOnFloor;
            for (var u = 0; u < unitsOnFloor && flatUnitIndex < createdUnits.Count; u++, flatUnitIndex++)
            {
                var unitFiles = Request.Form.Files.GetFiles($"unitImages_F{f}_U{u}");
                if (unitFiles.Count == 0) continue;

                var unit = createdUnits[flatUnitIndex];
                var unitDir = Path.Combine(_env.WebRootPath, "uploads", "units", unit.UnitId.ToString());
                Directory.CreateDirectory(unitDir);
                int unitImgOrder = 0;
                foreach (var file in unitFiles.Where(ff => ff.Length > 0))
                {
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!AllowedImgExt.Contains(ext)) continue;
                    var name = $"{Guid.NewGuid()}{ext}";
                    await using var fs = System.IO.File.Create(Path.Combine(unitDir, name));
                    await file.CopyToAsync(fs);
                    _db.UnitImages.Add(new UnitImage
                    {
                        UnitId    = unit.UnitId,
                        ImagePath = $"/uploads/units/{unit.UnitId}/{name}",
                        SortOrder = unitImgOrder++
                    });
                }
            }
        }
        await _db.SaveChangesAsync();

        // Save uploaded property images
        if (model.Images is { Count: > 0 })
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "properties", property.PropertyId.ToString());
            Directory.CreateDirectory(dir);
            int order = 0;
            foreach (var file in model.Images.Where(f => f.Length > 0))
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedImgExt.Contains(ext)) continue;
                var name = $"{Guid.NewGuid()}{ext}";
                await using var fs = System.IO.File.Create(Path.Combine(dir, name));
                await file.CopyToAsync(fs);
                _db.PropertyImages.Add(new PropertyImage
                {
                    PropertyId = property.PropertyId,
                    ImagePath  = $"/uploads/properties/{property.PropertyId}/{name}",
                    SortOrder  = order++
                });
            }
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = $"Property added successfully with {unitNumbers.Count} unit(s).";
        return RedirectToAction("Units", new { propertyId = property.PropertyId });
    }

    // GET /Properties/Edit/{id}
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> Edit(int id)
    {
        var property = await _db.Properties
            .Include(p => p.PropertyAmenities)
            .ThenInclude(pa => pa.Amenity)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PropertyId == id);
        if (property == null) return NotFound();

        var propertyAmenityNames = AmenityLinkService.GetPropertyAmenityNames(property);
        var amenitySource = AmenityLinkService.JoinDisplayNames(propertyAmenityNames);

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

        foreach (var propertyAmenityError in PropertyAmenitySelection.ValidateCustomAmenityList(model.CustomAmenities))
            ModelState.AddModelError(nameof(model.CustomAmenities), propertyAmenityError);

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
        await AmenityLinkService.SyncPropertyAmenitiesAsync(_db, entity.PropertyId, mergedAmenities);

        TempData["Success"] = "Property updated successfully.";
        return RedirectToAction(nameof(Manage));
    }

    // POST /Properties/Delete/{id}
    [Authorize(Roles = "PropertyManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        // Return the user to whichever page they deleted from (Index cards or Manage table).
        IActionResult BackToOrigin()
        {
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) &&
                Uri.TryCreate(referer, UriKind.Absolute, out var refUri) &&
                Url.IsLocalUrl(refUri.PathAndQuery))
            {
                return Redirect(refUri.PathAndQuery);
            }
            return RedirectToAction(nameof(Manage));
        }

        var property = await _db.Properties.FirstOrDefaultAsync(p => p.PropertyId == id);
        if (property == null) return NotFound();

        var unitIds = await _db.Units.Where(u => u.PropertyId == id).Select(u => u.UnitId).ToListAsync();

        try
        {
            var (ok, error) = await PropertyUnitDeletionHelper.TryCascadeDeleteUnitsAsync(_db, unitIds);
            if (!ok)
            {
                TempData["Error"] = error;
                return BackToOrigin();
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

        return BackToOrigin();
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
