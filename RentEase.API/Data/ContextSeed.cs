using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;

namespace PropertyLeasing.API.Data;

public static class ContextSeed
{
    // ── Entry point ───────────────────────────────────────────────────────────
    public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                         .CreateLogger("ContextSeed");
        try
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var db          = serviceProvider.GetRequiredService<PropertyLeasingDbContext>();

            foreach (var role in new[] { "PropertyManager", "Tenant", "MaintenanceStaff" })
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            // ── Managers ──
            await SeedUser(userManager, db, "manager@propleasing.com",  "Ahmed Al-Mansoori", "Manager@123", "PropertyManager", "66600001", logger);

            // ── Tenants ──
            await SeedUser(userManager, db, "tenant1@example.com",        "Sara Al-Khalifa",    "Tenant@123",  "Tenant", "66600002", logger);
            await SeedUser(userManager, db, "tenant2@example.com",        "Mohammed Al-Baker",  "Tenant@123",  "Tenant", "66600003", logger);
            await SeedUser(userManager, db, "tenant3@example.com",        "Noor Ibrahim",       "Tenant@123",  "Tenant", "66600004", logger);
            await SeedUser(userManager, db, "tenant4@example.com",        "Fatima Al-Zayani",   "Tenant@123",  "Tenant", "66600005", logger);
            await SeedUser(userManager, db, "tenant5@example.com",        "Omar Al-Rumaihi",    "Tenant@123",  "Tenant", "66600006", logger);
            await SeedUser(userManager, db, "murtadhaahss05@gmail.com",   "Murtadha",           "12345678aA@", "Tenant", null,        logger);

            // ── Maintenance Staff (2 per category) ──
            await SeedUser(userManager, db, "staff1@propleasing.com",  "Ali Hassan",          "Staff@123", "MaintenanceStaff", "66600007", logger); // Plumbing
            await SeedUser(userManager, db, "staff2@propleasing.com",  "Yusuf Al-Darwish",    "Staff@123", "MaintenanceStaff", "66600008", logger); // Plumbing
            await SeedUser(userManager, db, "staff3@propleasing.com",  "Khalid Al-Hamad",     "Staff@123", "MaintenanceStaff", "66600009", logger); // Electrical
            await SeedUser(userManager, db, "staff4@propleasing.com",  "Mariam Al-Mutawa",    "Staff@123", "MaintenanceStaff", "66600010", logger); // Electrical
            await SeedUser(userManager, db, "staff5@propleasing.com",  "Faisal Al-Najjar",    "Staff@123", "MaintenanceStaff", "66600011", logger); // HVAC
            await SeedUser(userManager, db, "staff6@propleasing.com",  "Laila Al-Sayed",      "Staff@123", "MaintenanceStaff", "66600012", logger); // HVAC
            await SeedUser(userManager, db, "staff7@propleasing.com",  "Hassan Al-Mahdi",     "Staff@123", "MaintenanceStaff", "66600013", logger); // Carpentry
            await SeedUser(userManager, db, "staff8@propleasing.com",  "Nora Al-Kooheji",     "Staff@123", "MaintenanceStaff", "66600014", logger); // Carpentry
            await SeedUser(userManager, db, "staff9@propleasing.com",  "Saeed Al-Qassim",     "Staff@123", "MaintenanceStaff", "66600015", logger); // General
            await SeedUser(userManager, db, "staff10@propleasing.com", "Reem Al-Madani",      "Staff@123", "MaintenanceStaff", "66600016", logger); // General

            // ── Staff profiles (idempotent/upsert — always safe to run) ──
            await SeedStaffProfilesAsync(db);
            await PatchMissingStaffProfilesAsync(db, logger);

            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var resetBusinessData = configuration.GetValue("Seed:ResetBusinessDataOnStartup", false);

            // ── Business data ────────────────────────────────────────────────────
            bool hasData = await db.Properties.AnyAsync();
            if (resetBusinessData && hasData)
            {
                logger.LogWarning("[Seed] Seed:ResetBusinessDataOnStartup=true — clearing business tables and reseeding.");
                await ResetBusinessDataAsync(db, logger);
                hasData = false;
            }

            if (!hasData)
            {
                logger.LogInformation("[Seed] Seeding business data (properties, units, applications, leases, …).");
                await SeedBusinessDataAsync(db, logger);
            }
            else
            {
                logger.LogInformation("[Seed] Business data already exists — skipping full seed (set Seed:ResetBusinessDataOnStartup=true to wipe & reseed).");
            }

            // ── Patches — idempotent, run every startup ──
            await PatchRegularApplicationsOffOccupiedUnitsAsync(db, logger);
            await PatchSyncAppUsersFromIdentityAsync(userManager, db, logger);
        }
        catch (Exception ex) { logger.LogError(ex, "[Seed] Top-level failure."); }
    }

    // ── Create / link Identity + App User (idempotent) ───────────────────────
    private static async Task SeedUser(
        UserManager<AppUser> userManager,
        PropertyLeasingDbContext db,
        string email, string fullName, string password,
        string role, string? phone, ILogger logger)
    {
        try
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing == null)
            {
                var identityUser = new AppUser
                {
                    UserName       = email,
                    Email          = email,
                    FullName       = fullName,
                    Phone          = phone,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(identityUser, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(identityUser, role);
                    existing = identityUser;
                }
                else
                {
                    logger.LogWarning("[Seed] Could not create identity user {Email}: {Errors}",
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            if (existing == null) return;

            // Always sync phone to Identity DB (no-op if already correct)
            if (!string.IsNullOrWhiteSpace(phone) && existing.Phone != phone)
            {
                existing.Phone = phone;
                await userManager.UpdateAsync(existing);
            }

            var appUser = await db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == existing.Id)
                       ?? await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (appUser == null)
            {
                db.Users.Add(new User
                {
                    FullName       = fullName,
                    Email          = email,
                    Phone          = phone,
                    Role           = role,
                    IdentityUserId = existing.Id
                });
                await db.SaveChangesAsync();
            }
            else if (appUser.IdentityUserId != existing.Id)
            {
                appUser.IdentityUserId = existing.Id;
                await db.SaveChangesAsync();
            }

            if (appUser != null && TenantProfileCatalog.TryGetByEmail(email, out var canonical))
            {
                var changed = false;
                if (!string.Equals(appUser.FullName, canonical.FullName, StringComparison.Ordinal))
                {
                    appUser.FullName = canonical.FullName;
                    changed = true;
                }
                if (phone != null && appUser.Phone != phone)
                {
                    appUser.Phone = phone;
                    changed = true;
                }
                if (existing.FullName != canonical.FullName)
                {
                    existing.FullName = canonical.FullName;
                    await userManager.UpdateAsync(existing);
                }
                if (changed)
                    await db.SaveChangesAsync();
            }
        }
        catch (Exception ex) { logger.LogError(ex, "[Seed] SeedUser failed for {Email}.", email); }
    }

    /// <summary>Aligns app <c>User</c> rows with ASP.NET Identity (fixes legacy SQL seed mismatches).</summary>
    private static async Task PatchSyncAppUsersFromIdentityAsync(
        UserManager<AppUser> userManager,
        PropertyLeasingDbContext db,
        ILogger logger)
    {
        var linked = await db.Users
            .Where(u => u.IdentityUserId != null && u.IdentityUserId != "")
            .ToListAsync();

        var updated = 0;
        foreach (var appUser in linked)
        {
            var identity = await userManager.FindByIdAsync(appUser.IdentityUserId!);
            if (identity == null) continue;

            var changed = TenantProfileSync.ApplyIdentityToAppUser(
                identity.FullName,
                identity.Email ?? appUser.Email,
                identity.Phone,
                v => appUser.FullName = v,
                v => appUser.Email = v,
                v => appUser.Phone = v,
                new TenantProfileSync.ProfileSnapshot(appUser.FullName, appUser.Email, appUser.Phone));

            if (changed)
                updated++;
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("[Patch] Synced {Count} app user profile(s) from Identity.", updated);
        }
    }

    private static async Task ResetBusinessDataAsync(PropertyLeasingDbContext db, ILogger logger)
    {
        logger.LogInformation("[Seed] Clearing existing business data...");

        await db.Database.ExecuteSqlRawAsync(@"
-- Break circular FK: LeaseApplication.ParentLeaseID ↔ Lease.RenewLeaseApplicationID
UPDATE [Lease]            SET RenewLeaseApplicationID = NULL WHERE RenewLeaseApplicationID IS NOT NULL;
UPDATE [LeaseApplication] SET ParentLeaseID           = NULL WHERE ParentLeaseID           IS NOT NULL;
DELETE FROM [LeaseLog];
DELETE FROM [MaintenanceStatusHistory];
IF OBJECT_ID('MaintenanceRequestLog') IS NOT NULL DELETE FROM [MaintenanceRequestLog];
DELETE FROM [PaymentRecord];
DELETE FROM [Document];
DELETE FROM [Lease];
DELETE FROM [LeaseApplicationLog];
DELETE FROM [MaintenanceRequest];
DELETE FROM [LeaseApplication];
DELETE FROM [Notification];
DELETE FROM [Feedback];
DELETE FROM [Log];
DELETE FROM [Unit];
DELETE FROM [Property];
DELETE FROM [Termination];
DELETE FROM [MaintenanceStaff];
        ");
    }

    // ── Create MaintenanceStaff profile rows (idempotent) ────────────────────
    private static async Task SeedStaffProfilesAsync(PropertyLeasingDbContext db)
    {
        var profiles = new[]
        {
            ("staff1@propleasing.com",  "Plumbing",   "Available"),
            ("staff2@propleasing.com",  "Plumbing",   "Available"),
            ("staff3@propleasing.com",  "Electrical", "Available"),
            ("staff4@propleasing.com",  "Electrical", "Available"),
            ("staff5@propleasing.com",  "HVAC",       "Available"),
            ("staff6@propleasing.com",  "HVAC",       "Available"),
            ("staff7@propleasing.com",  "Carpentry",  "Available"),
            ("staff8@propleasing.com",  "Carpentry",  "Available"),
            ("staff9@propleasing.com",  "General",    "Available"),
            ("staff10@propleasing.com", "General",    "Available"),
        };

        foreach (var (email, skills, avail) in profiles)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) continue;

            var existingProfile = await db.MaintenanceStaffs.FirstOrDefaultAsync(s => s.UserId == user.UserId);
            if (existingProfile == null)
            {
                db.MaintenanceStaffs.Add(new MaintenanceStaff
                {
                    UserId              = user.UserId,
                    SkillProfile        = skills,
                    AvailabilityStatus  = avail
                });
            }
            else
            {
                existingProfile.SkillProfile = skills;
                existingProfile.AvailabilityStatus = avail;
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Ensures every MaintenanceStaff user has a profile row so category filters can match skills.
    /// </summary>
    private static async Task PatchMissingStaffProfilesAsync(PropertyLeasingDbContext db, ILogger logger)
    {
        var staffUserIds = await db.Users
            .Where(u => u.Role == "MaintenanceStaff")
            .Select(u => u.UserId)
            .ToListAsync();

        var profileUserIds = await db.MaintenanceStaffs.Select(s => s.UserId).ToListAsync();
        var missing = staffUserIds.Except(profileUserIds).ToList();
        if (missing.Count == 0) return;

        foreach (var userId in missing)
        {
            db.MaintenanceStaffs.Add(new MaintenanceStaff
            {
                UserId             = userId,
                SkillProfile       = "General",
                AvailabilityStatus = "Available"
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("[Seed] Created {Count} missing maintenance staff profile(s).", missing.Count);
    }

    // ── Business data (skips if 10+ properties already exist) ────────────────
    private static async Task SeedBusinessDataAsync(PropertyLeasingDbContext db, ILogger logger)
    {
        if (await db.Properties.CountAsync() >= 10) return;
        logger.LogInformation("[Seed] Starting business data seed...");

        var mgr  = await db.Users.FirstOrDefaultAsync(u => u.Email == "manager@propleasing.com");
        var t1   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant1@example.com");
        var t2   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant2@example.com");
        var t3   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant3@example.com");
        var t4   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant4@example.com");
        var t5   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant5@example.com");
        // Plumbing staff
        var st1  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff1@propleasing.com");
        var st2  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff2@propleasing.com");
        // Electrical staff
        var st3  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff3@propleasing.com");
        var st4  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff4@propleasing.com");
        // HVAC staff
        var st5  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff5@propleasing.com");
        var st6  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff6@propleasing.com");
        // Carpentry staff
        var st7  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff7@propleasing.com");
        var st8  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff8@propleasing.com");
        // General staff
        var st9  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff9@propleasing.com");
        var st10 = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff10@propleasing.com");

        if (mgr == null || t1 == null || t2 == null || t3 == null ||
            t4 == null || t5 == null || st1 == null || st2 == null || st3 == null ||
            st4 == null || st5 == null || st6 == null || st7 == null || st8 == null ||
            st9 == null || st10 == null)
            return;

        // ── 10 Properties ──────────────────────────────────────────────────────
        var seef = Prop("Seef Tower Residences",
            "Modern high-rise in Seef District offering studios to 3BR apartments with sea views and premium amenities.",
            "Building 1040, Road 3621, Block 336", "Manama", "Residential");

        var riffa = Prop("Riffa Hills Apartments",
            "Spacious family apartments in tranquil Northern Riffa, close to Riffa Views and major shopping centres.",
            "Building 325, Road 1216, Block 912", "Riffa", "Residential");

        var juffair = Prop("Juffair Bay Complex",
            "Mixed-use complex in vibrant Juffair with residential units and retail spaces.",
            "Building 2156, Road 4561, Block 445", "Manama", "Mixed");

        var adliya = Prop("Adliya Garden Residences",
            "Boutique residential building in the arts district of Adliya, walking distance to galleries and restaurants.",
            "Building 561, Road 3819, Block 338", "Manama", "Residential");

        var amwaj = Prop("Amwaj Waterfront Living",
            "Premium waterfront apartments on Amwaj Islands with private beach access and marina views.",
            "Building 88, Amwaj Avenue, Island 3", "Muharraq", "Residential");

        var budaiya = Prop("Budaiya Family Residences",
            "Quiet suburban apartments in Budaiya ideal for families, with green spaces and easy highway access.",
            "Building 210, Road 5512, Block 551", "Budaiya", "Residential");

        var diplomatic = Prop("Diplomatic Quarter Suites",
            "Executive suites and serviced apartments in the prestigious Diplomatic Area, steps from embassies.",
            "Building 14, Diplomatic Road, Block 317", "Manama", "Mixed");

        var busaiteen = Prop("Busaiteen Bay Apartments",
            "Affordable modern apartments in Busaiteen offering excellent connectivity to Bahrain International Airport.",
            "Building 730, Road 2215, Block 222", "Muharraq", "Residential");

        var tubli = Prop("Tubli Business Park",
            "Contemporary commercial units, offices and showrooms in Tubli's growing business corridor.",
            "Building 92, Road 4108, Block 410", "Tubli", "Commercial");

        var muharraq = Prop("Muharraq Heritage Homes",
            "Renovated heritage-style family apartments in Old Muharraq, blending traditional architecture with modern comforts.",
            "Building 45, Shaikh Isa Road, Block 212", "Muharraq", "Residential");

        db.Properties.AddRange(seef, riffa, juffair, adliya, amwaj,
                               budaiya, diplomatic, busaiteen, tubli, muharraq);
        await db.SaveChangesAsync();

        // ── Units (20 per property) ────────────────────────────────────────────
        // Helper: U(propertyId, number, type, bedrooms, sqm, rent, status, amenities)

        // ── Seef Tower (20 units) ──
        var seefUnits = new[]
        {
            U(seef.PropertyId, "S101", "Studio",    0,  48,  210m, "Occupied",  "Central AC, Fitted Kitchen, Internet"),
            U(seef.PropertyId, "S102", "Studio",    0,  50,  220m, "Available", "Central AC, Fitted Kitchen, Gym Access"),
            U(seef.PropertyId, "S103", "Studio",    0,  46,  200m, "Available", "Central AC, Fitted Kitchen"),
            U(seef.PropertyId, "S104", "Studio",    0,  52,  230m, "Available", "Central AC, Sea View, Internet"),
            U(seef.PropertyId, "A101", "Apartment", 1,  70,  300m, "Occupied",  "Balcony, Parking, Central AC"),
            U(seef.PropertyId, "A102", "Apartment", 1,  72,  310m, "Available", "Balcony, Central AC, Internet"),
            U(seef.PropertyId, "A103", "Apartment", 1,  68,  290m, "Available", "Central AC, Parking"),
            U(seef.PropertyId, "A104", "Apartment", 1,  75,  320m, "Available", "Sea View, Central AC, Gym"),
            U(seef.PropertyId, "A105", "Apartment", 1,  71,  305m, "Occupied",  "Balcony, Parking, Central AC"),
            U(seef.PropertyId, "B101", "Apartment", 2, 115,  450m, "Occupied",  "Balcony, Parking x2, Gym, Pool"),
            U(seef.PropertyId, "B102", "Apartment", 2, 118,  460m, "Available", "Sea View, Balcony, Parking, Gym"),
            U(seef.PropertyId, "B103", "Apartment", 2, 112,  440m, "Available", "Balcony, Parking, Central AC"),
            U(seef.PropertyId, "B104", "Apartment", 2, 120,  470m, "Available", "Sea View, Parking x2, Pool"),
            U(seef.PropertyId, "B105", "Apartment", 2, 110,  430m, "Available", "Balcony, Central AC, Gym"),
            U(seef.PropertyId, "C101", "Apartment", 3, 170,  680m, "Occupied",  "Sea View, Balcony, Parking x2, Pool"),
            U(seef.PropertyId, "C102", "Apartment", 3, 175,  700m, "Available", "Sea View, Balcony, Parking x2, Gym"),
            U(seef.PropertyId, "C103", "Apartment", 3, 165,  660m, "Available", "Balcony, Parking x2, Central AC"),
            U(seef.PropertyId, "O101", "Office",    0, 120,  550m, "Available", "Open Plan, Meeting Room, Pantry"),
            U(seef.PropertyId, "O102", "Office",    0, 150,  700m, "Available", "Private Offices, Conference Room, 24/7"),
            U(seef.PropertyId, "O103", "Office",    0,  90,  420m, "UnderMaintenance", "Open Plan, Pantry"),
        };

        // ── Riffa Hills (20 units) ──
        var riffaUnits = new[]
        {
            U(riffa.PropertyId, "R101", "Apartment", 1,  65,  190m, "Occupied",  "Garden View, Parking, Central AC"),
            U(riffa.PropertyId, "R102", "Apartment", 1,  67,  195m, "Available", "Central AC, Parking"),
            U(riffa.PropertyId, "R103", "Apartment", 1,  63,  185m, "Available", "Garden View, Central AC"),
            U(riffa.PropertyId, "R104", "Apartment", 1,  70,  205m, "Available", "Balcony, Central AC, Parking"),
            U(riffa.PropertyId, "R105", "Studio",    0,  45,  165m, "Available", "Fitted Kitchen, Central AC"),
            U(riffa.PropertyId, "R106", "Studio",    0,  48,  170m, "Available", "Fitted Kitchen, Central AC, Internet"),
            U(riffa.PropertyId, "R201", "Apartment", 2, 105,  360m, "Occupied",  "Garden, Parking x2, Central AC"),
            U(riffa.PropertyId, "R202", "Apartment", 2, 110,  375m, "Available", "Balcony, Parking, Central AC"),
            U(riffa.PropertyId, "R203", "Apartment", 2, 108,  368m, "Available", "Garden View, Parking, Central AC"),
            U(riffa.PropertyId, "R204", "Apartment", 2, 112,  385m, "Available", "Balcony, Parking x2, Storage"),
            U(riffa.PropertyId, "R205", "Apartment", 2, 106,  365m, "Occupied",  "Garden View, Parking, Central AC, Maid Room"),
            U(riffa.PropertyId, "R206", "Apartment", 2, 115,  395m, "Available", "Balcony, Parking x2, Gym"),
            U(riffa.PropertyId, "R301", "Apartment", 3, 150,  520m, "Occupied",  "Garden, Parking x2, Maid Room, Storage"),
            U(riffa.PropertyId, "R302", "Apartment", 3, 155,  535m, "Available", "Balcony, Parking x2, Central AC"),
            U(riffa.PropertyId, "R303", "Apartment", 3, 148,  510m, "Available", "Garden View, Parking x2"),
            U(riffa.PropertyId, "R304", "Apartment", 3, 160,  550m, "Available", "Corner Unit, Parking x2, Maid Room"),
            U(riffa.PropertyId, "R401", "Apartment", 4, 200,  720m, "Available", "Garden, Parking x3, Maid Room, Pool"),
            U(riffa.PropertyId, "R402", "Apartment", 4, 195,  700m, "Available", "Balcony, Parking x3, Maid Room"),
            U(riffa.PropertyId, "R403", "Apartment", 4, 205,  740m, "Available", "Corner Unit, Garden, Parking x3"),
            U(riffa.PropertyId, "R404", "Apartment", 4, 210,  760m, "Available", "Penthouse, Parking x3, Private Terrace"),
        };

        // ── Juffair Bay (20 units) ──
        var juffairUnits = new[]
        {
            U(juffair.PropertyId, "J101", "Studio",    0,  50,  230m, "Available", "Sea View, Central AC, Internet"),
            U(juffair.PropertyId, "J102", "Studio",    0,  48,  220m, "Available", "Central AC, Fitted Kitchen"),
            U(juffair.PropertyId, "J103", "Studio",    0,  52,  240m, "Occupied",  "Sea View, Central AC, Gym"),
            U(juffair.PropertyId, "J104", "Studio",    0,  46,  210m, "Available", "Central AC, Internet"),
            U(juffair.PropertyId, "J201", "Apartment", 1,  72,  290m, "Occupied",  "Balcony, Parking, Central AC"),
            U(juffair.PropertyId, "J202", "Apartment", 1,  75,  300m, "Available", "Sea View, Balcony, Parking"),
            U(juffair.PropertyId, "J203", "Apartment", 1,  70,  280m, "Available", "Central AC, Parking"),
            U(juffair.PropertyId, "J204", "Apartment", 1,  78,  310m, "Available", "Sea View, Balcony, Gym"),
            U(juffair.PropertyId, "J301", "Apartment", 2, 115,  460m, "Occupied",  "Balcony, Parking, Gym, Pool"),
            U(juffair.PropertyId, "J302", "Apartment", 2, 118,  470m, "Available", "Sea View, Parking x2, Gym"),
            U(juffair.PropertyId, "J303", "Apartment", 2, 112,  445m, "Available", "Balcony, Parking, Central AC"),
            U(juffair.PropertyId, "J304", "Apartment", 2, 120,  480m, "Available", "Sea View, Parking x2, Pool"),
            U(juffair.PropertyId, "J305", "Apartment", 2, 110,  440m, "Available", "Balcony, Parking, Gym"),
            U(juffair.PropertyId, "J401", "Apartment", 3, 170,  690m, "Available", "Sea View, Balcony, Parking x2, Pool"),
            U(juffair.PropertyId, "J402", "Apartment", 3, 165,  670m, "Available", "Balcony, Parking x2, Gym"),
            U(juffair.PropertyId, "SP01", "Shop",      0,  60,  380m, "Available", "Street Frontage, Storage, 3-Phase Power"),
            U(juffair.PropertyId, "SP02", "Shop",      0,  75,  460m, "Available", "Corner Unit, Storage, Air Conditioning"),
            U(juffair.PropertyId, "SP03", "Shop",      0,  55,  350m, "UnderMaintenance", "Storage, Air Conditioning"),
            U(juffair.PropertyId, "SP04", "Shop",      0,  80,  490m, "Available", "Main Road, Storage, 3-Phase Power"),
            U(juffair.PropertyId, "SP05", "Shop",      0,  65,  400m, "Available", "Street Frontage, Storage"),
        };

        // ── Adliya Garden (20 units) ──
        var adliyaUnits = new[]
        {
            U(adliya.PropertyId, "AD101", "Studio",    0,  50,  220m, "Available", "Garden View, Central AC, Internet"),
            U(adliya.PropertyId, "AD102", "Studio",    0,  52,  230m, "Occupied",  "Central AC, Fitted Kitchen, Parking"),
            U(adliya.PropertyId, "AD103", "Studio",    0,  48,  215m, "Available", "Garden View, Central AC"),
            U(adliya.PropertyId, "AD104", "Studio",    0,  55,  245m, "Available", "Central AC, Gym Access, Internet"),
            U(adliya.PropertyId, "AD105", "Studio",    0,  47,  210m, "Available", "Central AC, Fitted Kitchen"),
            U(adliya.PropertyId, "AD201", "Apartment", 1,  70,  295m, "Occupied",  "Balcony, Parking, Central AC"),
            U(adliya.PropertyId, "AD202", "Apartment", 1,  73,  305m, "Available", "Garden View, Parking, Central AC"),
            U(adliya.PropertyId, "AD203", "Apartment", 1,  68,  285m, "Available", "Balcony, Central AC, Internet"),
            U(adliya.PropertyId, "AD204", "Apartment", 1,  76,  315m, "Available", "Garden View, Parking, Gym"),
            U(adliya.PropertyId, "AD205", "Apartment", 1,  71,  300m, "Available", "Balcony, Parking, Central AC"),
            U(adliya.PropertyId, "AD301", "Apartment", 2, 108,  420m, "Occupied",  "Balcony, Parking, Central AC, Storage"),
            U(adliya.PropertyId, "AD302", "Apartment", 2, 112,  435m, "Available", "Garden View, Parking x2, Central AC"),
            U(adliya.PropertyId, "AD303", "Apartment", 2, 106,  415m, "Available", "Balcony, Parking, Central AC"),
            U(adliya.PropertyId, "AD304", "Apartment", 2, 115,  445m, "Available", "Garden View, Parking x2, Gym"),
            U(adliya.PropertyId, "AD305", "Apartment", 2, 110,  430m, "Available", "Corner Unit, Parking, Central AC"),
            U(adliya.PropertyId, "AD306", "Apartment", 2, 118,  450m, "Available", "Balcony, Parking x2, Storage"),
            U(adliya.PropertyId, "AD401", "Apartment", 3, 155,  570m, "Available", "Balcony, Parking x2, Central AC, Storage"),
            U(adliya.PropertyId, "AD402", "Apartment", 3, 160,  585m, "Available", "Garden View, Parking x2, Gym"),
            U(adliya.PropertyId, "AD403", "Apartment", 3, 150,  555m, "Available", "Balcony, Parking x2, Central AC"),
            U(adliya.PropertyId, "AD404", "Apartment", 3, 165,  600m, "Available", "Corner Unit, Parking x2, Maid Room"),
        };

        // ── Amwaj Waterfront (20 units) ──
        var amwajUnits = new[]
        {
            U(amwaj.PropertyId, "AM101", "Studio",    0,  55,  280m, "Available", "Sea View, Central AC, Beach Access"),
            U(amwaj.PropertyId, "AM102", "Studio",    0,  58,  295m, "Occupied",  "Marina View, Central AC, Gym"),
            U(amwaj.PropertyId, "AM103", "Studio",    0,  52,  265m, "Available", "Sea View, Central AC, Internet"),
            U(amwaj.PropertyId, "AM104", "Studio",    0,  60,  305m, "Available", "Marina View, Central AC, Beach Access"),
            U(amwaj.PropertyId, "AM201", "Apartment", 1,  80,  380m, "Occupied",  "Sea View, Balcony, Parking, Pool"),
            U(amwaj.PropertyId, "AM202", "Apartment", 1,  82,  390m, "Available", "Marina View, Balcony, Parking, Gym"),
            U(amwaj.PropertyId, "AM203", "Apartment", 1,  78,  370m, "Available", "Sea View, Balcony, Parking, Beach"),
            U(amwaj.PropertyId, "AM204", "Apartment", 1,  85,  400m, "Available", "Marina View, Balcony, Parking x2"),
            U(amwaj.PropertyId, "AM205", "Apartment", 1,  80,  380m, "Available", "Sea View, Balcony, Pool, Gym"),
            U(amwaj.PropertyId, "AM301", "Apartment", 2, 125,  550m, "Occupied",  "Sea View, Balcony, Parking x2, Pool"),
            U(amwaj.PropertyId, "AM302", "Apartment", 2, 128,  560m, "Available", "Marina View, Balcony, Parking x2, Gym"),
            U(amwaj.PropertyId, "AM303", "Apartment", 2, 122,  535m, "Available", "Sea View, Balcony, Parking, Beach"),
            U(amwaj.PropertyId, "AM304", "Apartment", 2, 130,  575m, "Available", "Corner Sea View, Parking x2, Pool"),
            U(amwaj.PropertyId, "AM305", "Apartment", 2, 125,  550m, "Available", "Marina View, Balcony, Parking x2"),
            U(amwaj.PropertyId, "AM401", "Apartment", 3, 185,  780m, "Available", "Panoramic Sea View, Balcony, Parking x2, Pool"),
            U(amwaj.PropertyId, "AM402", "Apartment", 3, 190,  800m, "Available", "Marina View, Balcony, Parking x2, Gym"),
            U(amwaj.PropertyId, "AM403", "Apartment", 3, 180,  760m, "Available", "Sea View, Balcony, Parking x2, Beach"),
            U(amwaj.PropertyId, "AM501", "Apartment", 4, 250, 1050m, "Available", "Penthouse, 360° Sea View, Parking x3, Private Pool"),
            U(amwaj.PropertyId, "AM502", "Apartment", 4, 240, 1000m, "Available", "Penthouse, Marina View, Parking x3, Roof Terrace"),
            U(amwaj.PropertyId, "AM503", "Apartment", 4, 245, 1025m, "Available", "Penthouse, Sea View, Parking x3, Private Gym"),
        };

        // ── Budaiya Family (20 units) ──
        var budaiyaUnits = new[]
        {
            U(budaiya.PropertyId, "BU101", "Apartment", 2, 105,  300m, "Occupied",  "Garden, Parking, Central AC, Maid Room"),
            U(budaiya.PropertyId, "BU102", "Apartment", 2, 108,  310m, "Available", "Garden View, Parking, Central AC"),
            U(budaiya.PropertyId, "BU103", "Apartment", 2, 103,  295m, "Available", "Garden, Parking, Central AC"),
            U(budaiya.PropertyId, "BU104", "Apartment", 2, 110,  320m, "Available", "Balcony, Parking x2, Central AC"),
            U(budaiya.PropertyId, "BU105", "Apartment", 2, 106,  305m, "Available", "Garden View, Parking, Storage"),
            U(budaiya.PropertyId, "BU201", "Apartment", 3, 145,  420m, "Occupied",  "Garden, Parking x2, Maid Room, Storage"),
            U(budaiya.PropertyId, "BU202", "Apartment", 3, 150,  435m, "Available", "Balcony, Parking x2, Central AC"),
            U(budaiya.PropertyId, "BU203", "Apartment", 3, 142,  410m, "Available", "Garden, Parking x2, Central AC"),
            U(budaiya.PropertyId, "BU204", "Apartment", 3, 155,  450m, "Available", "Corner Unit, Parking x2, Maid Room"),
            U(budaiya.PropertyId, "BU205", "Apartment", 3, 148,  425m, "Available", "Garden View, Parking x2, Storage"),
            U(budaiya.PropertyId, "BU206", "Apartment", 3, 152,  440m, "Available", "Balcony, Parking x2, Central AC"),
            U(budaiya.PropertyId, "BU207", "Apartment", 3, 145,  420m, "Occupied",  "Garden, Parking x2, Central AC, Maid Room"),
            U(budaiya.PropertyId, "BU208", "Apartment", 3, 158,  460m, "Available", "Corner, Parking x2, Private Yard"),
            U(budaiya.PropertyId, "BU301", "Apartment", 4, 195,  600m, "Available", "Garden, Parking x3, Maid Room, Pool"),
            U(budaiya.PropertyId, "BU302", "Apartment", 4, 200,  620m, "Available", "Corner Garden, Parking x3, Maid Room"),
            U(budaiya.PropertyId, "BU303", "Apartment", 4, 192,  585m, "Available", "Garden, Parking x3, Storage"),
            U(budaiya.PropertyId, "BU304", "Apartment", 4, 205,  640m, "Available", "Premium Corner, Parking x3, Maid Room, Pool"),
            U(budaiya.PropertyId, "BU305", "Apartment", 4, 198,  610m, "Available", "Garden, Parking x3, Maid Room"),
            U(budaiya.PropertyId, "BU306", "Apartment", 4, 202,  630m, "Available", "Corner, Parking x3, Private Terrace"),
            U(budaiya.PropertyId, "BU307", "Apartment", 4, 210,  660m, "Available", "Premium, Garden, Parking x4, Pool"),
        };

        // ── Diplomatic Quarter (20 units) ──
        var diplomaticUnits = new[]
        {
            U(diplomatic.PropertyId, "DQ101", "Studio",    0,  58,  310m, "Available", "Executive Finish, Central AC, 24/7 Security"),
            U(diplomatic.PropertyId, "DQ102", "Studio",    0,  60,  320m, "Occupied",  "City View, Central AC, Concierge"),
            U(diplomatic.PropertyId, "DQ103", "Studio",    0,  55,  295m, "Available", "Central AC, Gym, 24/7 Security"),
            U(diplomatic.PropertyId, "DQ201", "Apartment", 1,  80,  420m, "Occupied",  "City View, Parking, Concierge, Gym"),
            U(diplomatic.PropertyId, "DQ202", "Apartment", 1,  82,  430m, "Available", "Balcony, Parking, Central AC, 24/7"),
            U(diplomatic.PropertyId, "DQ203", "Apartment", 1,  78,  410m, "Available", "City View, Parking, Gym"),
            U(diplomatic.PropertyId, "DQ204", "Apartment", 1,  85,  440m, "Available", "Executive Finish, Parking, Concierge"),
            U(diplomatic.PropertyId, "DQ205", "Apartment", 1,  80,  420m, "Available", "City View, Parking, Central AC"),
            U(diplomatic.PropertyId, "DQ206", "Apartment", 1,  83,  435m, "Available", "Balcony, Parking x2, Gym, Concierge"),
            U(diplomatic.PropertyId, "DQ301", "Apartment", 2, 130,  650m, "Occupied",  "City View, Balcony, Parking x2, Gym"),
            U(diplomatic.PropertyId, "DQ302", "Apartment", 2, 135,  670m, "Available", "Executive, Parking x2, Concierge, Pool"),
            U(diplomatic.PropertyId, "DQ303", "Apartment", 2, 128,  635m, "Available", "City View, Parking x2, Gym"),
            U(diplomatic.PropertyId, "DQ304", "Apartment", 2, 140,  690m, "Available", "Corner, Parking x2, Executive Finish"),
            U(diplomatic.PropertyId, "OF101", "Office",    0, 100,  600m, "Available", "Open Plan, Meeting Rooms, 24/7 Access"),
            U(diplomatic.PropertyId, "OF102", "Office",    0, 120,  720m, "Available", "Partitioned, Executive Suite, 24/7"),
            U(diplomatic.PropertyId, "OF103", "Office",    0,  80,  480m, "Available", "Open Plan, Pantry, Security"),
            U(diplomatic.PropertyId, "OF104", "Office",    0, 150,  900m, "Available", "Full Floor, Board Room, Executive"),
            U(diplomatic.PropertyId, "OF105", "Office",    0,  90,  540m, "Occupied",  "Partitioned, Meeting Room, 24/7"),
            U(diplomatic.PropertyId, "OF106", "Office",    0, 110,  660m, "Available", "Open Plan, Reception, 24/7 Access"),
            U(diplomatic.PropertyId, "OF107", "Office",    0, 200, 1200m, "Available", "Full Floor Premium, Board Room, Lounge"),
        };

        // ── Busaiteen Bay (20 units) ──
        var busaiteen_units = new[]
        {
            U(busaiteen.PropertyId, "BS101", "Studio",    0,  44,  155m, "Available", "Central AC, Fitted Kitchen"),
            U(busaiteen.PropertyId, "BS102", "Studio",    0,  46,  160m, "Available", "Central AC, Internet"),
            U(busaiteen.PropertyId, "BS103", "Studio",    0,  48,  168m, "Occupied",  "Sea View, Central AC, Parking"),
            U(busaiteen.PropertyId, "BS104", "Studio",    0,  45,  158m, "Available", "Central AC, Fitted Kitchen"),
            U(busaiteen.PropertyId, "BS105", "Studio",    0,  50,  175m, "Available", "Sea View, Central AC"),
            U(busaiteen.PropertyId, "BS201", "Apartment", 1,  65,  240m, "Occupied",  "Balcony, Parking, Central AC"),
            U(busaiteen.PropertyId, "BS202", "Apartment", 1,  67,  248m, "Available", "Sea View, Central AC, Internet"),
            U(busaiteen.PropertyId, "BS203", "Apartment", 1,  63,  232m, "Available", "Balcony, Parking, Central AC"),
            U(busaiteen.PropertyId, "BS204", "Apartment", 1,  70,  258m, "Available", "Sea View, Parking, Gym"),
            U(busaiteen.PropertyId, "BS205", "Apartment", 1,  65,  240m, "Available", "Balcony, Central AC"),
            U(busaiteen.PropertyId, "BS206", "Apartment", 1,  68,  250m, "Available", "Sea View, Parking, Internet"),
            U(busaiteen.PropertyId, "BS207", "Apartment", 1,  66,  244m, "Available", "Balcony, Parking, Central AC"),
            U(busaiteen.PropertyId, "BS301", "Apartment", 2, 105,  360m, "Available", "Balcony, Parking, Central AC"),
            U(busaiteen.PropertyId, "BS302", "Apartment", 2, 108,  370m, "Available", "Sea View, Parking x2, Gym"),
            U(busaiteen.PropertyId, "BS303", "Apartment", 2, 103,  352m, "Available", "Balcony, Parking, Central AC"),
            U(busaiteen.PropertyId, "BS304", "Apartment", 2, 110,  380m, "Available", "Sea View, Parking x2, Pool"),
            U(busaiteen.PropertyId, "BS305", "Apartment", 2, 106,  365m, "Available", "Balcony, Parking, Storage"),
            U(busaiteen.PropertyId, "BS306", "Apartment", 2, 112,  388m, "Available", "Sea View, Parking x2, Gym"),
            U(busaiteen.PropertyId, "BS401", "Apartment", 3, 148,  490m, "Available", "Balcony, Parking x2, Central AC"),
            U(busaiteen.PropertyId, "BS402", "Apartment", 3, 152,  505m, "Available", "Sea View, Parking x2, Gym"),
        };

        // ── Tubli Business Park (20 units - commercial) ──
        var tubliUnits = new[]
        {
            U(tubli.PropertyId, "TB101", "Shop",   0,  55,  280m, "Available", "Street Front, Storage, Air Conditioning"),
            U(tubli.PropertyId, "TB102", "Shop",   0,  60,  300m, "Available", "Corner Unit, Storage, 3-Phase Power"),
            U(tubli.PropertyId, "TB103", "Shop",   0,  50,  255m, "Available", "Street Front, Storage"),
            U(tubli.PropertyId, "TB104", "Shop",   0,  70,  350m, "Occupied",  "Street Front, Large Storage, 3-Phase"),
            U(tubli.PropertyId, "TB105", "Shop",   0,  65,  328m, "Available", "Corner, Storage, Air Conditioning"),
            U(tubli.PropertyId, "TB106", "Shop",   0,  48,  245m, "Available", "Street Front, Storage"),
            U(tubli.PropertyId, "TB107", "Shop",   0,  80,  400m, "Available", "Corner, Large Storage, 3-Phase Power"),
            U(tubli.PropertyId, "TB108", "Shop",   0,  55,  280m, "Available", "Street Front, Storage, AC"),
            U(tubli.PropertyId, "TB109", "Shop",   0,  58,  292m, "UnderMaintenance", "Street Front, Storage"),
            U(tubli.PropertyId, "TB110", "Shop",   0,  62,  312m, "Available", "Corner, Storage, Air Conditioning"),
            U(tubli.PropertyId, "OF201", "Office", 0,  90,  420m, "Occupied",  "Open Plan, Meeting Room, Pantry"),
            U(tubli.PropertyId, "OF202", "Office", 0, 100,  468m, "Available", "Partitioned, Meeting Room, 24/7"),
            U(tubli.PropertyId, "OF203", "Office", 0,  80,  374m, "Available", "Open Plan, Pantry"),
            U(tubli.PropertyId, "OF204", "Office", 0, 120,  562m, "Available", "Executive Suite, Board Room, 24/7"),
            U(tubli.PropertyId, "OF205", "Office", 0,  95,  445m, "Available", "Partitioned, Meeting Room"),
            U(tubli.PropertyId, "OF206", "Office", 0, 110,  515m, "Available", "Open Plan, Reception, 24/7"),
            U(tubli.PropertyId, "OF207", "Office", 0,  85,  398m, "Available", "Open Plan, Pantry, Security"),
            U(tubli.PropertyId, "OF208", "Office", 0, 150,  703m, "Available", "Full Floor, Board Room, Executive"),
            U(tubli.PropertyId, "OF209", "Office", 0, 130,  609m, "Available", "Partitioned, Meeting Rooms, 24/7"),
            U(tubli.PropertyId, "OF210", "Office", 0, 200,  937m, "Available", "Premium Floor, Lounge, Board Room"),
        };

        // ── Muharraq Heritage (20 units) ──
        var muharraqUnits = new[]
        {
            U(muharraq.PropertyId, "MH101", "Apartment", 2, 100,  280m, "Occupied",  "Heritage Design, Courtyard, Parking"),
            U(muharraq.PropertyId, "MH102", "Apartment", 2, 105,  292m, "Available", "Heritage Design, Parking, Central AC"),
            U(muharraq.PropertyId, "MH103", "Apartment", 2,  98,  274m, "Available", "Renovated, Parking, Central AC"),
            U(muharraq.PropertyId, "MH104", "Apartment", 2, 108,  302m, "Available", "Corner, Heritage Design, Parking x2"),
            U(muharraq.PropertyId, "MH105", "Apartment", 2, 102,  285m, "Available", "Heritage Style, Parking, Internet"),
            U(muharraq.PropertyId, "MH201", "Apartment", 3, 140,  380m, "Occupied",  "Heritage Design, Courtyard, Parking x2"),
            U(muharraq.PropertyId, "MH202", "Apartment", 3, 145,  395m, "Available", "Renovated, Parking x2, Central AC"),
            U(muharraq.PropertyId, "MH203", "Apartment", 3, 138,  372m, "Available", "Heritage, Courtyard, Parking x2"),
            U(muharraq.PropertyId, "MH204", "Apartment", 3, 150,  408m, "Available", "Corner, Heritage Design, Parking x2"),
            U(muharraq.PropertyId, "MH205", "Apartment", 3, 143,  388m, "Available", "Renovated, Parking x2, Central AC"),
            U(muharraq.PropertyId, "MH206", "Apartment", 3, 148,  402m, "Available", "Heritage Style, Courtyard, Parking x2"),
            U(muharraq.PropertyId, "MH207", "Apartment", 3, 142,  385m, "Available", "Renovated, Corner, Parking x2"),
            U(muharraq.PropertyId, "MH301", "Apartment", 4, 190,  540m, "Available", "Heritage, Large Courtyard, Parking x3"),
            U(muharraq.PropertyId, "MH302", "Apartment", 4, 195,  555m, "Available", "Renovated, Parking x3, Maid Room"),
            U(muharraq.PropertyId, "MH303", "Apartment", 4, 188,  530m, "Available", "Heritage Design, Parking x3, Central AC"),
            U(muharraq.PropertyId, "MH304", "Apartment", 4, 200,  575m, "Available", "Premium Heritage, Parking x3, Private Terrace"),
            U(muharraq.PropertyId, "MH305", "Apartment", 4, 192,  548m, "Available", "Renovated, Parking x3, Maid Room"),
            U(muharraq.PropertyId, "MH306", "Apartment", 4, 198,  568m, "Available", "Heritage Style, Parking x3, Courtyard"),
            U(muharraq.PropertyId, "MH307", "Apartment", 4, 205,  590m, "Available", "Corner Premium, Parking x4, Maid Room"),
            U(muharraq.PropertyId, "MH308", "Apartment", 4, 210,  608m, "Available", "Top Floor Heritage, Roof Terrace, Parking x4"),
        };

        db.Units.AddRange(seefUnits);
        db.Units.AddRange(riffaUnits);
        db.Units.AddRange(juffairUnits);
        db.Units.AddRange(adliyaUnits);
        db.Units.AddRange(amwajUnits);
        db.Units.AddRange(budaiyaUnits);
        db.Units.AddRange(diplomaticUnits);
        db.Units.AddRange(busaiteen_units);
        db.Units.AddRange(tubliUnits);
        db.Units.AddRange(muharraqUnits);
        await db.SaveChangesAsync();


        var tM = await db.Users.FirstOrDefaultAsync(u => u.Email == "murtadhaahss05@gmail.com");
        await LeaseApplicationSeedData.SeedAsync(db, logger,
            new LeaseApplicationSeedData.Users
            {
                Manager = mgr, T1 = t1, T2 = t2, T3 = t3, T4 = t4, T5 = t5, Murtadha = tM
            },
            new LeaseApplicationSeedData.Units
            {
                Seef = seefUnits, Riffa = riffaUnits, Juffair = juffairUnits, Adliya = adliyaUnits,
                Amwaj = amwajUnits, Budaiya = budaiyaUnits, Diplomatic = diplomaticUnits,
                Busaiteen = busaiteen_units, Muharraq = muharraqUnits, JuffairProperty = juffair
            });


        // ── Maintenance Requests (category matches assigned staff skill) ─────────
        // Submitted — no staff yet
        var mr1 = new MaintenanceRequest { UnitId = juffairUnits[4].UnitId,      TenantUserId = t3.UserId,  Title = "Broken Window Latch",        Description = "Bedroom window latch broken, cannot lock window securely.",        RequestType = "Carpentry",  Priority = "Low",    Status = "Submitted",  TicketNumber = "TKT-2026-001", SubmittedAt = DateTime.Today.AddDays(-1) };
        var mr2 = new MaintenanceRequest { UnitId = diplomaticUnits[3].UnitId,   TenantUserId = t4.UserId,  Title = "Intercom Not Functioning",   Description = "Building intercom system not responding in unit.",                 RequestType = "Electrical", Priority = "Medium", Status = "Submitted",  TicketNumber = "TKT-2026-002", SubmittedAt = DateTime.Today.AddDays(-1) };
        var mr3 = new MaintenanceRequest { UnitId = amwajUnits[4].UnitId,        TenantUserId = t5.UserId,  Title = "Balcony Door Stiff",         Description = "Sliding balcony door hard to open/close, needs lubrication.",      RequestType = "General",    Priority = "Low",    Status = "Submitted",  TicketNumber = "TKT-2026-003", SubmittedAt = DateTime.Today };

        // Assigned — staff assigned, work not started yet
        var mr4 = new MaintenanceRequest { UnitId = riffaUnits[0].UnitId,        TenantUserId = t2.UserId,  AssignedStaffId = st2.UserId, Title = "Kitchen Sink Leaking",       Description = "Drain pipe under sink leaking, water damage to cabinet floor.",  RequestType = "Plumbing",   Priority = "Medium", Status = "Assigned",   TicketNumber = "TKT-2026-004", SubmittedAt = DateTime.Today.AddDays(-5) };
        var mr5 = new MaintenanceRequest { UnitId = busaiteen_units[5].UnitId,   TenantUserId = t5.UserId,  AssignedStaffId = st7.UserId, Title = "Cabinet Door Hinge Broken",  Description = "Kitchen cabinet door hinge snapped off, door dangling loose.",    RequestType = "Carpentry",  Priority = "Medium", Status = "Assigned",   TicketNumber = "TKT-2026-005", SubmittedAt = DateTime.Today.AddDays(-3) };

        // InProgress — staff actively working
        var mr6 = new MaintenanceRequest { UnitId = seefUnits[0].UnitId,         TenantUserId = t1.UserId,  AssignedStaffId = st5.UserId, Title = "AC Unit Malfunction",        Description = "Bedroom AC stopped cooling. Room temperature exceeds 35°C.",      RequestType = "HVAC",       Priority = "High",   Status = "InProgress", TicketNumber = "TKT-2026-006", SubmittedAt = DateTime.Today.AddDays(-3) };
        var mr7 = new MaintenanceRequest { UnitId = adliyaUnits[1].UnitId,       TenantUserId = t4.UserId,  AssignedStaffId = st3.UserId, Title = "Electrical Socket Sparking", Description = "Living room socket sparks dangerously when plugging appliances.",  RequestType = "Electrical", Priority = "High",   Status = "InProgress", TicketNumber = "TKT-2026-007", SubmittedAt = DateTime.Today.AddDays(-2) };

        // Resolved — work completed
        var mr8 = new MaintenanceRequest { UnitId = budaiyaUnits[0].UnitId,      TenantUserId = t1.UserId,  AssignedStaffId = st1.UserId, Title = "Water Heater Not Working",   Description = "Hot water unavailable. Pilot light keeps going out.",             RequestType = "Plumbing",   Priority = "High",   Status = "Resolved",   TicketNumber = "TKT-2026-008", SubmittedAt = DateTime.Today.AddDays(-10), ResolvedAt = DateTime.Today.AddDays(-7), ResolutionNotes = "Thermocouple replaced. Water heater fully operational." };
        var mr9 = new MaintenanceRequest { UnitId = muharraqUnits[0].UnitId,     TenantUserId = t1.UserId,  AssignedStaffId = st6.UserId, Title = "HVAC Filter Replacement",    Description = "Air quality poor, HVAC filter clogged and needs replacement.",    RequestType = "HVAC",       Priority = "Medium", Status = "Resolved",   TicketNumber = "TKT-2026-009", SubmittedAt = DateTime.Today.AddDays(-14), ResolvedAt = DateTime.Today.AddDays(-11), ResolutionNotes = "HEPA filter replaced and system serviced." };

        // Closed — fully closed after resolution
        var mr10 = new MaintenanceRequest { UnitId = busaiteen_units[5].UnitId,  TenantUserId = t5.UserId,  AssignedStaffId = st9.UserId, Title = "Bathroom Tiles Cracked",     Description = "Three floor tiles cracked — safety hazard, needs replacement.",   RequestType = "General",    Priority = "Medium", Status = "Closed",     TicketNumber = "TKT-2025-088", SubmittedAt = DateTime.Today.AddMonths(-2), ResolvedAt = DateTime.Today.AddMonths(-2).AddDays(3), ResolutionNotes = "Cracked tiles replaced with matching stock." };

        db.MaintenanceRequests.AddRange(mr1, mr2, mr3, mr4, mr5, mr6, mr7, mr8, mr9, mr10);
        await db.SaveChangesAsync();

        // ── Status History — every request starts with "Submitted" ──────────────
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr1.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr1.SubmittedAt,  ChangedByUserId = t3.UserId },
            new MaintenanceStatusHistory { RequestId = mr2.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr2.SubmittedAt,  ChangedByUserId = t4.UserId },
            new MaintenanceStatusHistory { RequestId = mr3.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr3.SubmittedAt,  ChangedByUserId = t5.UserId },
            new MaintenanceStatusHistory { RequestId = mr4.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr4.SubmittedAt,  ChangedByUserId = t2.UserId },
            new MaintenanceStatusHistory { RequestId = mr5.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr5.SubmittedAt,  ChangedByUserId = t5.UserId },
            new MaintenanceStatusHistory { RequestId = mr6.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr6.SubmittedAt,  ChangedByUserId = t1.UserId },
            new MaintenanceStatusHistory { RequestId = mr7.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant.",                                       ChangedAt = mr7.SubmittedAt,  ChangedByUserId = t4.UserId },
            new MaintenanceStatusHistory { RequestId = mr8.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant. Category: Plumbing. Priority: High.",  ChangedAt = mr8.SubmittedAt,  ChangedByUserId = t1.UserId },
            new MaintenanceStatusHistory { RequestId = mr9.RequestId,  OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant. Category: HVAC. Priority: Medium.",    ChangedAt = mr9.SubmittedAt,  ChangedByUserId = t1.UserId },
            new MaintenanceStatusHistory { RequestId = mr10.RequestId, OldStatus = null, NewStatus = "Submitted", Notes = "Request submitted by tenant. Category: General. Priority: Medium.", ChangedAt = mr10.SubmittedAt, ChangedByUserId = t5.UserId }
        );
        await db.SaveChangesAsync();

        // ── Status History for non-Submitted requests ─────────────────────────
        // Assigned requests: Submitted → Assigned
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr4.RequestId,  OldStatus = "Submitted", NewStatus = "Assigned",    Notes = "Staff assigned to investigate leak.",           ChangedAt = mr4.SubmittedAt.AddDays(1),  ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr5.RequestId,  OldStatus = "Submitted", NewStatus = "Assigned",    Notes = "Carpentry staff scheduled for inspection.",     ChangedAt = mr5.SubmittedAt.AddDays(1),  ChangedByUserId = mgr.UserId }
        );
        // InProgress requests: Submitted → Assigned → InProgress
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr6.RequestId,  OldStatus = "Submitted",  NewStatus = "Assigned",   Notes = "HVAC specialist assigned.",                     ChangedAt = mr6.SubmittedAt.AddDays(1),  ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr6.RequestId,  OldStatus = "Assigned",   NewStatus = "InProgress", Notes = "Staff on-site, diagnosing AC fault.",            ChangedAt = mr6.SubmittedAt.AddDays(2),  ChangedByUserId = st5.UserId },
            new MaintenanceStatusHistory { RequestId = mr7.RequestId,  OldStatus = "Submitted",  NewStatus = "Assigned",   Notes = "Electrician dispatched immediately.",             ChangedAt = mr7.SubmittedAt.AddHours(4), ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr7.RequestId,  OldStatus = "Assigned",   NewStatus = "InProgress", Notes = "Socket panel opened, fault located.",             ChangedAt = mr7.SubmittedAt.AddDays(1),  ChangedByUserId = st3.UserId }
        );
        // Resolved requests: full progression
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr8.RequestId,  OldStatus = "Submitted",  NewStatus = "Assigned",   Notes = "Plumber assigned.",                              ChangedAt = mr8.SubmittedAt.AddDays(1),  ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr8.RequestId,  OldStatus = "Assigned",   NewStatus = "InProgress", Notes = "Inspecting water heater.",                       ChangedAt = mr8.SubmittedAt.AddDays(2),  ChangedByUserId = st1.UserId },
            new MaintenanceStatusHistory { RequestId = mr8.RequestId,  OldStatus = "InProgress", NewStatus = "Resolved",   Notes = "Thermocouple replaced. Hot water restored.",     ChangedAt = mr8.ResolvedAt!.Value,       ChangedByUserId = st1.UserId },
            new MaintenanceStatusHistory { RequestId = mr9.RequestId,  OldStatus = "Submitted",  NewStatus = "Assigned",   Notes = "HVAC technician scheduled.",                     ChangedAt = mr9.SubmittedAt.AddDays(1),  ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr9.RequestId,  OldStatus = "Assigned",   NewStatus = "InProgress", Notes = "Filter inspection underway.",                    ChangedAt = mr9.SubmittedAt.AddDays(2),  ChangedByUserId = st6.UserId },
            new MaintenanceStatusHistory { RequestId = mr9.RequestId,  OldStatus = "InProgress", NewStatus = "Resolved",   Notes = "HEPA filter replaced, system serviced.",         ChangedAt = mr9.ResolvedAt!.Value,       ChangedByUserId = st6.UserId }
        );
        // Closed request: full progression + Closed
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr10.RequestId, OldStatus = "Submitted",  NewStatus = "Assigned",   Notes = "General maintenance staff dispatched.",          ChangedAt = mr10.SubmittedAt.AddDays(1), ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr10.RequestId, OldStatus = "Assigned",   NewStatus = "InProgress", Notes = "Tile removal in progress.",                      ChangedAt = mr10.SubmittedAt.AddDays(2), ChangedByUserId = st9.UserId },
            new MaintenanceStatusHistory { RequestId = mr10.RequestId, OldStatus = "InProgress", NewStatus = "Resolved",   Notes = mr10.ResolutionNotes,                             ChangedAt = mr10.ResolvedAt!.Value,      ChangedByUserId = st9.UserId },
            new MaintenanceStatusHistory { RequestId = mr10.RequestId, OldStatus = "Resolved",   NewStatus = "Closed",     Notes = "Tenant confirmed issue resolved. Ticket closed.", ChangedAt = mr10.ResolvedAt!.Value.AddDays(1), ChangedByUserId = mgr.UserId }
        );
        await db.SaveChangesAsync();


        // ── Maintenance & payment notifications ───────────────────────────────
        db.Notifications.AddRange(
            Notif(t1.UserId,  "Maintenance request TKT-2026-001 has been received.",                         "MaintenanceUpdate"),
            Notif(t2.UserId,  "Maintenance request TKT-2026-002 has been assigned to a technician.",       "MaintenanceUpdate"),
            Notif(t1.UserId,  "Maintenance request TKT-2026-006 has been resolved.",                         "MaintenanceUpdate"),
            Notif(t5.UserId,  "Maintenance request TKT-2025-088 has been resolved and closed.",             "MaintenanceUpdate"),
            Notif(mgr.UserId, "New maintenance ticket submitted: Electrical Socket Sparking (AD102).",    "MaintenanceUpdate"),
            Notif(t1.UserId,  "Payment of BD 220 is due for unit S101. Please arrange payment.",           "PaymentReminder"),
            Notif(t2.UserId,  "Payment of BD 190 for unit R101 is overdue. Please contact the manager.",   "PaymentReminder"),
            Notif(t3.UserId,  "Payment of BD 290 is due for unit J201.",                                   "PaymentReminder")
        );
        await db.SaveChangesAsync();
    }

    // ── Small builder helpers ─────────────────────────────────────────────────
    private static Property Prop(string name, string desc, string address, string city, string type) =>
        new Property { Name = name, Description = desc, Address = address, City = city, PropertyType = type };

    private static Unit U(int propertyId, string number, string type, int bedrooms,
        double sqm, decimal rent, string status, string amenities) =>
        new Unit
        {
            PropertyId         = propertyId,
            UnitNumber         = number,
            UnitType           = bedrooms == 0 ? type : $"{bedrooms}BR {type}",
            Sizesqm            = sqm,
            MonthlyRent        = rent,
            AvailabilityStatus = status,
            Amenities          = amenities
        };


    private static Notification Notif(int userId, string message, string type) =>
        new Notification { UserId = userId, Message = message, NotificationType = type, Status = "Unread", CreatedAt = DateTime.Now };

    /// <summary>
    /// Moves non-renewal Pending/Screening applications off Occupied units onto an Available unit in the same property.
    /// Fixes legacy seed data without requiring a full database reset.
    /// </summary>
    private static async Task PatchRegularApplicationsOffOccupiedUnitsAsync(
        PropertyLeasingDbContext db,
        ILogger logger)
    {
        var candidates = await db.LeaseApplications
            .Include(a => a.Unit)
            .Where(a => a.ParentLeaseId == null &&
                        (a.Status == "Pending" || a.Status == "Screening"))
            .ToListAsync();

        var changed = 0;
        foreach (var app in candidates)
        {
            if (!LeaseApplicationSeedRules.RegularPendingOrScreeningOnOccupiedUnit(
                    app.ParentLeaseId, app.Status, app.Unit.AvailabilityStatus))
                continue;

            var replacement = await db.Units
                .Where(u =>
                    u.PropertyId == app.Unit.PropertyId &&
                    string.Equals(u.AvailabilityStatus, "Available", StringComparison.Ordinal) &&
                    u.UnitId != app.UnitId)
                .OrderBy(u => u.UnitNumber)
                .FirstOrDefaultAsync();

            if (replacement == null)
            {
                logger.LogWarning(
                    "[Patch] Application {AppId}: no Available unit in property {PropId} — cannot move off Occupied unit {UnitNo}.",
                    app.ApplicationId, app.Unit.PropertyId, app.Unit.UnitNumber);
                continue;
            }

            logger.LogInformation(
                "[Patch] Application {AppId}: moved from Occupied {OldNo} → Available {NewNo} (same property).",
                app.ApplicationId, app.Unit.UnitNumber, replacement.UnitNumber);

            app.UnitId = replacement.UnitId;
            changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync();
    }
}
