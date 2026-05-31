using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;
using Xunit;

namespace PropertyLeasing.API.Tests;

public class AmenityLinkServiceTests
{
    private static PropertyLeasingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PropertyLeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PropertyLeasingDbContext(options);
    }

    [Fact]
    public async Task SyncPropertyAmenitiesAsync_CreatesCompositeLinks()
    {
        await using var db = CreateDb();
        var property = new Property
        {
            Name = "Test Tower",
            Address = "123 Test St",
            City = "Manama"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        await AmenityLinkService.SyncPropertyAmenitiesAsync(db, property.PropertyId, new[] { "Parking", "Gym" });

        var links = await db.PropertyAmenities
            .Include(pa => pa.Amenity)
            .Where(pa => pa.PropertyId == property.PropertyId)
            .ToListAsync();

        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.Amenity.Name == "Parking");
        Assert.Contains(links, l => l.Amenity.Name == "Gym");
        Assert.All(links, l => Assert.Equal(property.PropertyId, l.PropertyId));
    }

    [Fact]
    public async Task SyncUnitAmenitiesAsync_ReplacesExistingLinks()
    {
        await using var db = CreateDb();
        var property = new Property { Name = "P", Address = "A", City = "C" };
        var unit = new Unit { Property = property, UnitNumber = "101", AvailabilityStatus = "Available" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        await AmenityLinkService.SyncUnitAmenitiesAsync(db, unit.UnitId, new[] { "Sea View" });
        await AmenityLinkService.SyncUnitAmenitiesAsync(db, unit.UnitId, new[] { "Corner Unit", "Sea View" });

        var names = await db.UnitAmenities
            .Include(ua => ua.Amenity)
            .Where(ua => ua.UnitId == unit.UnitId)
            .Select(ua => ua.Amenity.Name)
            .ToListAsync();

        Assert.Equal(2, names.Count);
        Assert.Contains("Sea View", names);
        Assert.Contains("Corner Unit", names);
    }

    [Fact]
    public async Task SyncPropertyAmenitiesAsync_RemovesLinksWhenNamesCleared()
    {
        await using var db = CreateDb();
        var property = new Property { Name = "P", Address = "A", City = "C" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        await AmenityLinkService.SyncPropertyAmenitiesAsync(db, property.PropertyId, new[] { "Pool" });
        await AmenityLinkService.SyncPropertyAmenitiesAsync(db, property.PropertyId, Array.Empty<string>());

        Assert.Empty(await db.PropertyAmenities.Where(pa => pa.PropertyId == property.PropertyId).ToListAsync());
    }

    [Fact]
    public void GetPropertyAndUnitAmenityNames_ReadFromNavigation()
    {
        var property = new Property
        {
            PropertyAmenities =
            {
                new PropertyAmenity { Amenity = new Amenity { Name = "Parking" } },
                new PropertyAmenity { Amenity = new Amenity { Name = "Gym" } }
            }
        };
        var unit = new Unit
        {
            UnitAmenities =
            {
                new UnitAmenity { Amenity = new Amenity { Name = "Sea View" } }
            }
        };

        Assert.Equal(new[] { "Gym", "Parking" }, AmenityLinkService.GetPropertyAmenityNames(property));
        Assert.Equal(new[] { "Sea View" }, AmenityLinkService.GetUnitAmenityNames(unit));
    }
}
