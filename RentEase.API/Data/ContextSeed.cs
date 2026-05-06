using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Models;

namespace PropertyLeasing.API.Data;

public static class ContextSeed
{
    // ── Entry point called from Program.cs ────────────────────────────────────
    public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var db          = serviceProvider.GetRequiredService<PropertyLeasingDbContext>();

            // Ensure roles exist
            foreach (var role in new[] { "PropertyManager", "Tenant", "MaintenanceStaff" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // ── Property Managers ──────────────────────────────────────────
            await SeedUser(userManager, db, "manager@propleasing.com",
                "Ahmed Al-Mansoori", "Manager@123", "PropertyManager", "+97317171001");

            // ── Tenants ────────────────────────────────────────────────────
            await SeedUser(userManager, db, "tenant1@example.com",
                "Sara Al-Khalifa",   "Tenant@123",  "Tenant", "+97333112233");
            await SeedUser(userManager, db, "tenant2@example.com",
                "Mohammed Al-Baker", "Tenant@123",  "Tenant", "+97333224455");
            await SeedUser(userManager, db, "tenant3@example.com",
                "Noor Ibrahim",      "Tenant@123",  "Tenant", "+97333667788");

            // ── Maintenance Staff ──────────────────────────────────────────
            await SeedUser(userManager, db, "staff1@propleasing.com",
                "Ali Hassan",        "Staff@123",   "MaintenanceStaff", "+97333445566",
                skillProfile: "Electrical, HVAC");
            await SeedUser(userManager, db, "staff2@propleasing.com",
                "Yusuf Al-Darwish",  "Staff@123",   "MaintenanceStaff", "+97333778899",
                skillProfile: "Plumbing, General");

            // ── Business data (properties, units, applications, leases…) ──
            await SeedBusinessDataAsync(db);
        }
        catch { /* swallow startup seed errors so the app always starts */ }
    }

    // ── Create / link an Identity + App User ──────────────────────────────────
    private static async Task SeedUser(
        UserManager<AppUser> userManager,
        PropertyLeasingDbContext db,
        string email,
        string fullName,
        string password,
        string role,
        string? phone = null,
        string? skillProfile = null)
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
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(identityUser, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(identityUser, role);
                    existing = identityUser;
                }
            }

            if (existing == null) return;

            // Link app-level User record
            if (!await db.Users.AnyAsync(u => u.IdentityUserId == existing.Id))
            {
                db.Users.Add(new User
                {
                    FullName          = fullName,
                    Email             = email,
                    Phone             = phone,
                    Role              = role,
                    SkillProfile      = skillProfile,
                    AvailabilityStatus = role == "MaintenanceStaff" ? "Available" : null,
                    IdentityUserId    = existing.Id
                });
                await db.SaveChangesAsync();
            }
        }
        catch { }
    }

    // ── Business data seed (idempotent: skips if any property exists) ─────────
    private static async Task SeedBusinessDataAsync(PropertyLeasingDbContext db)
    {
        if (await db.Properties.AnyAsync()) return;

        // Fetch app-level user IDs by email
        var mgr  = await db.Users.FirstOrDefaultAsync(u => u.Email == "manager@propleasing.com");
        var t1   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant1@example.com");
        var t2   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant2@example.com");
        var t3   = await db.Users.FirstOrDefaultAsync(u => u.Email == "tenant3@example.com");
        var st1  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff1@propleasing.com");
        var st2  = await db.Users.FirstOrDefaultAsync(u => u.Email == "staff2@propleasing.com");

        if (mgr == null || t1 == null || t2 == null || t3 == null || st1 == null || st2 == null)
            return;

        // ── Properties ────────────────────────────────────────────────────────
        var seef = new Property
        {
            Name         = "Seef Tower Residences",
            Description  = "Modern residential tower in the heart of Seef District, offering a range of apartment sizes with premium finishes and sea views.",
            Address      = "Building 1040, Road 3621, Block 336",
            City         = "Manama",
            PropertyType = "Residential"
        };
        var riffa = new Property
        {
            Name         = "Riffa Hills Apartments",
            Description  = "Spacious family apartments in the tranquil Northern Riffa area, close to Riffa Views and major shopping centres.",
            Address      = "Building 325, Road 1216, Block 912",
            City         = "Riffa",
            PropertyType = "Residential"
        };
        var juffair = new Property
        {
            Name         = "Juffair Bay Complex",
            Description  = "Mixed-use complex in vibrant Juffair, featuring residential units and retail spaces, steps from major amenities.",
            Address      = "Building 2156, Road 4561, Block 445",
            City         = "Manama",
            PropertyType = "Mixed"
        };

        db.Properties.AddRange(seef, riffa, juffair);
        await db.SaveChangesAsync();

        // ── Units ─────────────────────────────────────────────────────────────
        // Seef Tower
        var uA101 = Unit(seef.PropertyId, "A101", "Apartment", 2,   120, 450,  "Available",     "Balcony, Parking, Gym Access, Central AC");
        var uA102 = Unit(seef.PropertyId, "A102", "Studio",    1,    55, 220,  "Occupied",      "Fitted Kitchen, Central AC, High-speed Internet");
        var uB101 = Unit(seef.PropertyId, "B101", "Apartment", 3,   185, 680,  "Available",     "Sea View, Balcony, Parking x2, Gym, Pool Access");
        var uB102 = Unit(seef.PropertyId, "B102", "Apartment", 1,    78, 320,  "Occupied",      "Central AC, Storage Room, Parking");
        var uC101 = Unit(seef.PropertyId, "C101", "Office",    0,   200, 850,  "Available",     "Open Plan, Meeting Room, Pantry, 24/7 Access");

        // Riffa Hills
        var uR101 = Unit(riffa.PropertyId, "R101", "Apartment", 2,  115, 380,  "Occupied",      "Garden View, Parking, Central AC, Maid's Room");
        var uR102 = Unit(riffa.PropertyId, "R102", "Studio",    1,   48, 175,  "Available",     "Central AC, Fitted Kitchen");
        var uR103 = Unit(riffa.PropertyId, "R103", "Apartment", 3,  155, 530,  "Available",     "Parking x2, Central AC, Storage, Balcony");

        // Juffair Bay
        var uJ101 = Unit(juffair.PropertyId, "J101", "Apartment", 1, 72, 290, "Available",     "Sea View, Central AC, High-speed Internet");
        var uJ102 = Unit(juffair.PropertyId, "J102", "Apartment", 2, 118, 460, "Available",    "Balcony, Parking, Gym Access, Central AC");
        var uJ201 = Unit(juffair.PropertyId, "J201", "Shop",      0,  65, 400, "UnderMaintenance", "Street Frontage, Storage Room, 3-phase Power");

        db.Units.AddRange(uA101, uA102, uB101, uB102, uC101,
                          uR101, uR102, uR103,
                          uJ101, uJ102, uJ201);
        await db.SaveChangesAsync();

        // ── Lease Applications ────────────────────────────────────────────────
        // 3 Approved (→ leases will be created)
        var appSaraA102 = LeaseApp(t1.UserId, uA102.UnitId,
            DateTime.Today.AddMonths(-6), DateTime.Today.AddMonths(6),
            "Approved", DateTime.Today.AddMonths(-7), "Long-term tenant, excellent references.");

        var appMohammedB102 = LeaseApp(t2.UserId, uB102.UnitId,
            DateTime.Today.AddMonths(-3), DateTime.Today.AddMonths(9),
            "Approved", DateTime.Today.AddMonths(-4), "Relocating from Manama for work.");

        var appNoorR101 = LeaseApp(t3.UserId, uR101.UnitId,
            DateTime.Today.AddMonths(-1), DateTime.Today.AddMonths(11),
            "Approved", DateTime.Today.AddMonths(-2), "Family of four, requires ground-floor unit.");

        // 1 Screening
        var appMohammedA101 = LeaseApp(t2.UserId, uA101.UnitId,
            DateTime.Today.AddDays(14), DateTime.Today.AddDays(14).AddMonths(10),
            "Screening", DateTime.Today.AddDays(-4), "Interested in a longer lease if possible.");

        // 2 Pending
        var appSaraB101 = LeaseApp(t1.UserId, uB101.UnitId,
            DateTime.Today.AddDays(10), DateTime.Today.AddDays(10).AddMonths(12),
            "Pending", DateTime.Today.AddDays(-2), "Sea-view preferred.");

        var appNoorJ102 = LeaseApp(t3.UserId, uJ102.UnitId,
            DateTime.Today.AddDays(7), DateTime.Today.AddDays(7).AddMonths(8),
            "Pending", DateTime.Today.AddDays(-1), null);

        // 1 Rejected
        var appNoorJ101 = LeaseApp(t3.UserId, uJ101.UnitId,
            DateTime.Today.AddDays(5), DateTime.Today.AddDays(5).AddMonths(6),
            "Rejected", DateTime.Today.AddDays(-12),
            "References could not be verified within the required timeframe.");

        db.LeaseApplications.AddRange(
            appSaraA102, appMohammedB102, appNoorR101,
            appMohammedA101,
            appSaraB101, appNoorJ102,
            appNoorJ101);
        await db.SaveChangesAsync();

        // ── Application Logs ──────────────────────────────────────────────────
        // Approved path: Pending → Screening → Approved
        AppLog(db, appSaraA102.ApplicationId,     "Pending",   t1.UserId,  appSaraA102.CreatedAt);
        AppLog(db, appSaraA102.ApplicationId,     "Screening", mgr.UserId, appSaraA102.CreatedAt.AddDays(2));
        AppLog(db, appSaraA102.ApplicationId,     "Approved",  mgr.UserId, appSaraA102.CreatedAt.AddDays(5));

        AppLog(db, appMohammedB102.ApplicationId, "Pending",   t2.UserId,  appMohammedB102.CreatedAt);
        AppLog(db, appMohammedB102.ApplicationId, "Screening", mgr.UserId, appMohammedB102.CreatedAt.AddDays(3));
        AppLog(db, appMohammedB102.ApplicationId, "Approved",  mgr.UserId, appMohammedB102.CreatedAt.AddDays(6));

        AppLog(db, appNoorR101.ApplicationId,     "Pending",   t3.UserId,  appNoorR101.CreatedAt);
        AppLog(db, appNoorR101.ApplicationId,     "Screening", mgr.UserId, appNoorR101.CreatedAt.AddDays(2));
        AppLog(db, appNoorR101.ApplicationId,     "Approved",  mgr.UserId, appNoorR101.CreatedAt.AddDays(4));

        // Screening path
        AppLog(db, appMohammedA101.ApplicationId, "Pending",   t2.UserId,  appMohammedA101.CreatedAt);
        AppLog(db, appMohammedA101.ApplicationId, "Screening", mgr.UserId, appMohammedA101.CreatedAt.AddDays(1));

        // Pending (just submitted)
        AppLog(db, appSaraB101.ApplicationId,     "Pending",   t1.UserId,  appSaraB101.CreatedAt);
        AppLog(db, appNoorJ102.ApplicationId,     "Pending",   t3.UserId,  appNoorJ102.CreatedAt);

        // Rejected path
        AppLog(db, appNoorJ101.ApplicationId,     "Pending",   t3.UserId,  appNoorJ101.CreatedAt);
        AppLog(db, appNoorJ101.ApplicationId,     "Screening", mgr.UserId, appNoorJ101.CreatedAt.AddDays(2));
        AppLog(db, appNoorJ101.ApplicationId,     "Rejected",  mgr.UserId, appNoorJ101.CreatedAt.AddDays(5));

        await db.SaveChangesAsync();

        // ── Leases ────────────────────────────────────────────────────────────
        var leaseSara = new Lease
        {
            ApplicationId   = appSaraA102.ApplicationId,
            LeaseStartDate  = DateTime.Today.AddMonths(-6),
            LeaseEndDate    = DateTime.Today.AddMonths(6),
            MonthlyRent     = 220,
            SecurityDeposit = 440,
            Status          = "Active",
            CreatedAt       = DateTime.Today.AddMonths(-6)
        };
        var leaseMohammed = new Lease
        {
            ApplicationId   = appMohammedB102.ApplicationId,
            LeaseStartDate  = DateTime.Today.AddMonths(-3),
            LeaseEndDate    = DateTime.Today.AddMonths(9),
            MonthlyRent     = 320,
            SecurityDeposit = 640,
            Status          = "Active",
            CreatedAt       = DateTime.Today.AddMonths(-3)
        };
        var leaseNoor = new Lease
        {
            ApplicationId   = appNoorR101.ApplicationId,
            LeaseStartDate  = DateTime.Today.AddMonths(-1),
            LeaseEndDate    = DateTime.Today.AddMonths(11),
            MonthlyRent     = 380,
            SecurityDeposit = 760,
            Status          = "Active",
            CreatedAt       = DateTime.Today.AddMonths(-1)
        };

        db.Leases.AddRange(leaseSara, leaseMohammed, leaseNoor);
        await db.SaveChangesAsync();

        // ── Lease Logs ────────────────────────────────────────────────────────
        db.LeaseLogs.AddRange(
            new LeaseLog { LeaseId = leaseSara.LeaseId,     Status = "Active", ChangedByUserId = mgr.UserId, Notes = "Lease created upon approval.", CreatedAt = leaseSara.CreatedAt },
            new LeaseLog { LeaseId = leaseMohammed.LeaseId, Status = "Active", ChangedByUserId = mgr.UserId, Notes = "Lease created upon approval.", CreatedAt = leaseMohammed.CreatedAt },
            new LeaseLog { LeaseId = leaseNoor.LeaseId,     Status = "Active", ChangedByUserId = mgr.UserId, Notes = "Lease created upon approval.", CreatedAt = leaseNoor.CreatedAt }
        );

        // ── Payment Records ───────────────────────────────────────────────────
        // Sara — 6 months paid + 1 current pending
        for (int m = 6; m >= 1; m--)
        {
            var due  = leaseSara.LeaseStartDate.AddMonths(6 - m);
            var paid = due.AddDays(3);
            db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = leaseSara.LeaseId,
                AmountDue     = 220,
                AmountPaid    = 220,
                DueDate       = due,
                PaidDate      = paid,
                PaymentStatus = "Paid",
                Notes         = "Bank transfer"
            });
        }
        db.PaymentRecords.Add(new PaymentRecord
        {
            LeaseId       = leaseSara.LeaseId,
            AmountDue     = 220,
            DueDate       = leaseSara.LeaseStartDate.AddMonths(6),
            PaymentStatus = "Pending"
        });

        // Mohammed — 2 months paid + 1 current pending
        for (int m = 3; m >= 2; m--)
        {
            var due  = leaseMohammed.LeaseStartDate.AddMonths(3 - m);
            var paid = due.AddDays(5);
            db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = leaseMohammed.LeaseId,
                AmountDue     = 320,
                AmountPaid    = 320,
                DueDate       = due,
                PaidDate      = paid,
                PaymentStatus = "Paid",
                Notes         = "Cash payment"
            });
        }
        // Overdue instalment (1 month ago, not yet paid)
        db.PaymentRecords.Add(new PaymentRecord
        {
            LeaseId       = leaseMohammed.LeaseId,
            AmountDue     = 320,
            DueDate       = leaseMohammed.LeaseStartDate.AddMonths(2),
            PaymentStatus = "Pending",
            Notes         = "Follow-up required"
        });

        // Noor — first instalment pending (just started)
        db.PaymentRecords.Add(new PaymentRecord
        {
            LeaseId       = leaseNoor.LeaseId,
            AmountDue     = 380,
            DueDate       = leaseNoor.LeaseStartDate,
            PaymentStatus = "Pending"
        });

        await db.SaveChangesAsync();

        // ── Maintenance Requests ──────────────────────────────────────────────
        var mr1 = new MaintenanceRequest
        {
            UnitId       = uA102.UnitId,
            TenantUserId = t1.UserId,
            Title        = "Air Conditioning Not Working",
            Description  = "The split AC unit in the bedroom has stopped cooling entirely. Room temperature is exceeding 35°C.",
            RequestType  = "HVAC",
            Priority     = "High",
            Status       = "Submitted",
            TicketNumber = "TKT-2026-001",
            SubmittedAt  = DateTime.Today.AddDays(-3)
        };
        var mr2 = new MaintenanceRequest
        {
            UnitId          = uB102.UnitId,
            TenantUserId    = t2.UserId,
            AssignedStaffId = st1.UserId,
            Title           = "Kitchen Sink Leaking",
            Description     = "The kitchen sink drain pipe is leaking under the cabinet, causing water damage to the base unit.",
            RequestType     = "Plumbing",
            Priority        = "Medium",
            Status          = "InProgress",
            TicketNumber    = "TKT-2026-002",
            SubmittedAt     = DateTime.Today.AddDays(-8),
        };
        var mr3 = new MaintenanceRequest
        {
            UnitId          = uR101.UnitId,
            TenantUserId    = t3.UserId,
            AssignedStaffId = st2.UserId,
            Title           = "Bathroom Floor Tiles Cracked",
            Description     = "Three bathroom floor tiles are cracked and present a safety hazard. Requires replacement.",
            RequestType     = "General",
            Priority        = "Low",
            Status          = "Resolved",
            TicketNumber    = "TKT-2026-003",
            SubmittedAt     = DateTime.Today.AddDays(-22),
            ResolvedAt      = DateTime.Today.AddDays(-15),
            ResolutionNotes = "All cracked tiles replaced with matching spares from building stock."
        };
        var mr4 = new MaintenanceRequest
        {
            UnitId          = uA102.UnitId,
            TenantUserId    = t1.UserId,
            AssignedStaffId = st2.UserId,
            Title           = "Front Door Lock Damaged",
            Description     = "The front door deadbolt lock is jammed and cannot be properly locked from the outside.",
            RequestType     = "General",
            Priority        = "Medium",
            Status          = "Closed",
            TicketNumber    = "TKT-2025-089",
            SubmittedAt     = DateTime.Today.AddMonths(-3),
            ResolvedAt      = DateTime.Today.AddMonths(-3).AddDays(2),
            ResolutionNotes = "Lock cylinder replaced. Tenant issued two new keys."
        };

        db.MaintenanceRequests.AddRange(mr1, mr2, mr3, mr4);
        await db.SaveChangesAsync();

        // ── Maintenance Status Histories ──────────────────────────────────────
        // mr2: Submitted → Assigned → InProgress
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr2.RequestId, OldStatus = null,         NewStatus = "Submitted", ChangedAt = mr2.SubmittedAt,           ChangedByUserId = t2.UserId  },
            new MaintenanceStatusHistory { RequestId = mr2.RequestId, OldStatus = "Submitted",  NewStatus = "Assigned",  ChangedAt = mr2.SubmittedAt.AddDays(1), ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr2.RequestId, OldStatus = "Assigned",   NewStatus = "InProgress", ChangedAt = mr2.SubmittedAt.AddDays(2), ChangedByUserId = st1.UserId }
        );
        // mr3: Submitted → Assigned → InProgress → Resolved
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr3.RequestId, OldStatus = null,          NewStatus = "Submitted",  ChangedAt = mr3.SubmittedAt,            ChangedByUserId = t3.UserId  },
            new MaintenanceStatusHistory { RequestId = mr3.RequestId, OldStatus = "Submitted",   NewStatus = "Assigned",   ChangedAt = mr3.SubmittedAt.AddDays(1), ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr3.RequestId, OldStatus = "Assigned",    NewStatus = "InProgress", ChangedAt = mr3.SubmittedAt.AddDays(2), ChangedByUserId = st2.UserId },
            new MaintenanceStatusHistory { RequestId = mr3.RequestId, OldStatus = "InProgress",  NewStatus = "Resolved",   ChangedAt = mr3.ResolvedAt!.Value,      ChangedByUserId = st2.UserId }
        );
        // mr4: full lifecycle to Closed
        db.MaintenanceStatusHistories.AddRange(
            new MaintenanceStatusHistory { RequestId = mr4.RequestId, OldStatus = null,          NewStatus = "Submitted",  ChangedAt = mr4.SubmittedAt,                  ChangedByUserId = t1.UserId  },
            new MaintenanceStatusHistory { RequestId = mr4.RequestId, OldStatus = "Submitted",   NewStatus = "Assigned",   ChangedAt = mr4.SubmittedAt.AddHours(4),       ChangedByUserId = mgr.UserId },
            new MaintenanceStatusHistory { RequestId = mr4.RequestId, OldStatus = "Assigned",    NewStatus = "InProgress", ChangedAt = mr4.SubmittedAt.AddHours(6),       ChangedByUserId = st2.UserId },
            new MaintenanceStatusHistory { RequestId = mr4.RequestId, OldStatus = "InProgress",  NewStatus = "Resolved",   ChangedAt = mr4.ResolvedAt!.Value,             ChangedByUserId = st2.UserId },
            new MaintenanceStatusHistory { RequestId = mr4.RequestId, OldStatus = "Resolved",    NewStatus = "Closed",     ChangedAt = mr4.ResolvedAt!.Value.AddDays(1),  ChangedByUserId = mgr.UserId }
        );

        await db.SaveChangesAsync();

        // ── Notifications ─────────────────────────────────────────────────────
        var notifications = new List<Notification>
        {
            Notif(t1.UserId,  "Your lease application for unit A102 has been approved. Welcome to Seef Tower!",  "LeaseUpdate"),
            Notif(t2.UserId,  "Your lease application for unit B102 has been approved.",                          "LeaseUpdate"),
            Notif(t3.UserId,  "Your lease application for unit R101 has been approved.",                          "LeaseUpdate"),
            Notif(t3.UserId,  "Your application for unit J101 has been rejected. Please contact us for details.", "LeaseUpdate"),
            Notif(t1.UserId,  "Maintenance request TKT-2026-001 has been received. We will review it shortly.",  "MaintenanceUpdate"),
            Notif(t2.UserId,  "Your maintenance request TKT-2026-002 is now In Progress.",                        "MaintenanceUpdate"),
            Notif(t3.UserId,  "Your maintenance request TKT-2026-003 has been resolved.",                         "MaintenanceUpdate"),
            Notif(mgr.UserId, "New lease application from Sara Al-Khalifa for unit B101.",                        "LeaseUpdate"),
            Notif(mgr.UserId, "New lease application from Noor Ibrahim for unit J102.",                           "LeaseUpdate"),
            Notif(mgr.UserId, "New maintenance request submitted: Air Conditioning Not Working (Unit A102).",     "MaintenanceUpdate"),
            Notif(t1.UserId,  "Payment of BD 220 is due for unit A102. Please arrange payment.",                  "PaymentReminder"),
            Notif(t2.UserId,  "Payment of BD 320 for unit B102 is overdue. Please contact the manager.",         "PaymentReminder"),
        };
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();
    }

    // ── Small builder helpers to reduce repetition ────────────────────────────
    private static Unit Unit(int propertyId, string number, string type, int bedrooms,
        double sqm, decimal rent, string status, string amenities) => new Unit
        {
            PropertyId         = propertyId,
            UnitNumber         = number,
            UnitType           = bedrooms == 0 ? type : $"{bedrooms}BR {type}",
            Sizesqm            = sqm,
            MonthlyRent        = rent,
            AvailabilityStatus = status,
            Amenities          = amenities
        };

    private static LeaseApplication LeaseApp(int userId, int unitId,
        DateTime start, DateTime end, string status, DateTime createdAt, string? notes) =>
        new LeaseApplication
        {
            UserId             = userId,
            UnitId             = unitId,
            RequestedStartDate = start,
            RequestedEndDate   = end,
            Status             = status,
            Notes              = notes,
            CreatedAt          = createdAt
        };

    private static void AppLog(PropertyLeasingDbContext db, int applicationId,
        string status, int changedByUserId, DateTime createdAt) =>
        db.LeaseApplicationLogs.Add(new LeaseApplicationLog
        {
            ApplicationId   = applicationId,
            Status          = status,
            ChangedByUserId = changedByUserId,
            CreatedAt       = createdAt
        });

    private static Notification Notif(int userId, string message, string type) =>
        new Notification
        {
            UserId           = userId,
            Message          = message,
            NotificationType = type,
            Status           = "Unread",
            CreatedAt        = DateTime.Now
        };
}
