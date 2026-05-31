using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Models;

namespace PropertyLeasing.BusinessLogic;

/// <summary>
/// Persists property and unit amenities through M:N junction tables.
/// </summary>
public static class AmenityLinkService
{
    public static IReadOnlyList<string> GetPropertyAmenityNames(Property property) =>
        (property.PropertyAmenities ?? Array.Empty<PropertyAmenity>())
            .Select(pa => pa.Amenity?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> GetUnitAmenityNames(Unit unit) =>
        (unit.UnitAmenities ?? Array.Empty<UnitAmenity>())
            .Select(ua => ua.Amenity?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string? JoinDisplayNames(IEnumerable<string>? names)
    {
        var list = (names ?? Enumerable.Empty<string>())
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return PropertyAmenitySelection.JoinForUnit(list);
    }

    public static async Task<IReadOnlyDictionary<string, Amenity>> EnsureAmenitiesAsync(
        DbContext db,
        IEnumerable<string>? names,
        CancellationToken cancellationToken = default)
    {
        var normalized = (names ?? Enumerable.Empty<string>())
            .Select(NormalizeName)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return new Dictionary<string, Amenity>(StringComparer.OrdinalIgnoreCase);

        var existing = await db.Set<Amenity>()
            .Where(a => normalized.Contains(a.Name))
            .ToListAsync(cancellationToken);

        var map = existing.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in normalized)
        {
            if (map.ContainsKey(name))
                continue;

            var amenity = new Amenity { Name = name };
            db.Set<Amenity>().Add(amenity);
            map[name] = amenity;
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);

        return map;
    }

    public static async Task SyncPropertyAmenitiesAsync(
        DbContext db,
        int propertyId,
        IEnumerable<string>? names,
        CancellationToken cancellationToken = default)
    {
        var desired = (names ?? Enumerable.Empty<string>())
            .Select(NormalizeName)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingLinks = await db.Set<PropertyAmenity>()
            .Include(pa => pa.Amenity)
            .Where(pa => pa.PropertyId == propertyId)
            .ToListAsync(cancellationToken);

        if (desired.Count == 0)
        {
            if (existingLinks.Count > 0)
            {
                db.Set<PropertyAmenity>().RemoveRange(existingLinks);
                await db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var amenityMap = await EnsureAmenitiesAsync(db, desired, cancellationToken);

        var toRemove = existingLinks
            .Where(link => link.Amenity == null || !desired.Contains(link.Amenity.Name))
            .ToList();
        if (toRemove.Count > 0)
            db.Set<PropertyAmenity>().RemoveRange(toRemove);

        var existingIds = existingLinks
            .Select(link => link.AmenityId)
            .ToHashSet();

        foreach (var name in desired)
        {
            if (!amenityMap.TryGetValue(name, out var amenity))
                continue;

            if (existingIds.Contains(amenity.AmenityId))
                continue;

            db.Set<PropertyAmenity>().Add(new PropertyAmenity
            {
                PropertyId = propertyId,
                AmenityId = amenity.AmenityId
            });
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task SyncUnitAmenitiesAsync(
        DbContext db,
        int unitId,
        IEnumerable<string>? names,
        CancellationToken cancellationToken = default)
    {
        var desired = (names ?? Enumerable.Empty<string>())
            .Select(NormalizeName)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingLinks = await db.Set<UnitAmenity>()
            .Include(ua => ua.Amenity)
            .Where(ua => ua.UnitId == unitId)
            .ToListAsync(cancellationToken);

        if (desired.Count == 0)
        {
            if (existingLinks.Count > 0)
            {
                db.Set<UnitAmenity>().RemoveRange(existingLinks);
                await db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var amenityMap = await EnsureAmenitiesAsync(db, desired, cancellationToken);

        var toRemove = existingLinks
            .Where(link => link.Amenity == null || !desired.Contains(link.Amenity.Name))
            .ToList();
        if (toRemove.Count > 0)
            db.Set<UnitAmenity>().RemoveRange(toRemove);

        var existingIds = existingLinks
            .Select(link => link.AmenityId)
            .ToHashSet();

        foreach (var name in desired)
        {
            if (!amenityMap.TryGetValue(name, out var amenity))
                continue;

            if (existingIds.Contains(amenity.AmenityId))
                continue;

            db.Set<UnitAmenity>().Add(new UnitAmenity
            {
                UnitId = unitId,
                AmenityId = amenity.AmenityId
            });
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task MigrateLegacyAmenityColumnsAsync(DbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return;

        if (await ColumnExistsAsync(db, "Property", "Amenities", cancellationToken))
        {
            var propertyRows = await db.Database
                .SqlQueryRaw<LegacyPropertyAmenityRow>(
                    "SELECT PropertyID AS PropertyId, Amenities FROM [Property] WHERE Amenities IS NOT NULL AND LTRIM(RTRIM(Amenities)) <> ''")
                .ToListAsync(cancellationToken);

            foreach (var row in propertyRows)
            {
                await SyncPropertyAmenitiesAsync(
                    db,
                    row.PropertyId,
                    PropertyAmenitySelection.ParseCommaSeparated(row.Amenities),
                    cancellationToken);
            }

            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE [Property] DROP COLUMN [Amenities];",
                cancellationToken);
        }

        if (await ColumnExistsAsync(db, "Unit", "Amenities", cancellationToken))
        {
            var unitRows = await db.Database
                .SqlQueryRaw<LegacyUnitAmenityRow>(
                    "SELECT UnitID AS UnitId, Amenities FROM [Unit] WHERE Amenities IS NOT NULL AND LTRIM(RTRIM(Amenities)) <> ''")
                .ToListAsync(cancellationToken);

            foreach (var row in unitRows)
            {
                await SyncUnitAmenitiesAsync(
                    db,
                    row.UnitId,
                    PropertyAmenitySelection.ParseCommaSeparated(row.Amenities),
                    cancellationToken);
            }

            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE [Unit] DROP COLUMN [Amenities];",
                cancellationToken);
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        DbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var result = await db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(1) AS [Value]
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = {0} AND COLUMN_NAME = {1}
                """,
                tableName,
                columnName)
            .SingleAsync(cancellationToken);

        return result > 0;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length > PropertyAmenitySelection.MaxCustomAmenityItemLength)
            trimmed = trimmed[..PropertyAmenitySelection.MaxCustomAmenityItemLength];
        return trimmed;
    }

    private sealed record LegacyPropertyAmenityRow(int PropertyId, string? Amenities);
    private sealed record LegacyUnitAmenityRow(int UnitId, string? Amenities);
}
