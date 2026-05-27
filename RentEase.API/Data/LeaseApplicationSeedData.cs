using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropertyLeasing.API.Models;
using PropertyLeasing.BusinessLogic;

namespace PropertyLeasing.API.Data;

/// <summary>Seed lease applications, supporting documents, leases, and related logs — aligned with current domain rules.</summary>
public static class LeaseApplicationSeedData
{
    public sealed class Users
    {
        public required User Manager { get; init; }
        public required User T1 { get; init; }
        public required User T2 { get; init; }
        public required User T3 { get; init; }
        public required User T4 { get; init; }
        public required User T5 { get; init; }
        public User? Murtadha { get; init; }
    }

    public sealed class Units
    {
        public required Unit[] Seef { get; init; }
        public required Unit[] Riffa { get; init; }
        public required Unit[] Juffair { get; init; }
        public required Unit[] Adliya { get; init; }
        public required Unit[] Amwaj { get; init; }
        public required Unit[] Budaiya { get; init; }
        public required Unit[] Diplomatic { get; init; }
        public required Unit[] Busaiteen { get; init; }
        public required Unit[] Muharraq { get; init; }
        public required Property JuffairProperty { get; init; }
    }

    public static async Task SeedAsync(PropertyLeasingDbContext db, ILogger logger, Users users, Units units)
    {
        var mgr = users.Manager;
        var t1 = users.T1;
        var t2 = users.T2;
        var t3 = users.T3;
        var t4 = users.T4;
        var t5 = users.T5;

        // ── Approved → active leases (Occupied units only) ───────────────────
        var app1 = App(t1.UserId, units.Seef[0].UnitId,   DateTime.Today.AddMonths(-8),  DateTime.Today.AddMonths(4),  "Approved", DateTime.Today.AddMonths(-9));
        var app2 = App(t2.UserId, units.Riffa[0].UnitId,  DateTime.Today.AddMonths(-5),  DateTime.Today.AddMonths(7),  "Approved", DateTime.Today.AddMonths(-6));
        var app3 = App(t3.UserId, units.Juffair[4].UnitId, DateTime.Today.AddMonths(-3),  DateTime.Today.AddMonths(9),  "Approved", DateTime.Today.AddMonths(-4));
        var app4 = App(t4.UserId, units.Adliya[1].UnitId, DateTime.Today.AddMonths(-2),  DateTime.Today.AddMonths(10), "Approved", DateTime.Today.AddMonths(-3));
        var app5 = App(t5.UserId, units.Amwaj[4].UnitId,  DateTime.Today.AddMonths(-1),  DateTime.Today.AddMonths(11), "Approved", DateTime.Today.AddMonths(-2));

        // Screening / Pending / Rejected — Available units only
        var appScreenOk = App(t2.UserId, units.Seef[5].UnitId,     DateTime.Today.AddDays(10), DateTime.Today.AddDays(10).AddMonths(10), "Screening", DateTime.Today.AddDays(-5));
        var appScreenRej = App(t3.UserId, units.Adliya[6].UnitId,   DateTime.Today.AddDays(14), DateTime.Today.AddDays(14).AddMonths(8),  "Screening", DateTime.Today.AddDays(-4));
        var appPending1 = App(t1.UserId, units.Juffair[1].UnitId,  DateTime.Today.AddDays(5),  DateTime.Today.AddDays(5).AddMonths(6),   "Pending",   DateTime.Today.AddDays(-1));
        var appPending2 = App(t4.UserId, units.Budaiya[6].UnitId,   DateTime.Today.AddDays(12), DateTime.Today.AddDays(12).AddMonths(9),  "Pending",   DateTime.Today);
        var appRejected = App(t5.UserId, units.Seef[15].UnitId,     DateTime.Today.AddDays(3),  DateTime.Today.AddDays(3).AddMonths(8),   "Rejected",  DateTime.Today.AddDays(-15));

        // Terminated history
        var appTerminated = App(t1.UserId, units.Riffa[6].UnitId, DateTime.Today.AddMonths(-14), DateTime.Today.AddMonths(-2), "Approved", DateTime.Today.AddMonths(-15));

        // Renewed lease + follow-up renewal application
        var appRenewed = App(t3.UserId, units.Budaiya[0].UnitId, DateTime.Today.AddMonths(-12), DateTime.Today.AddMonths(0), "Approved", DateTime.Today.AddMonths(-13));

        db.LeaseApplications.AddRange(
            app1, app2, app3, app4, app5,
            appScreenOk, appScreenRej, appPending1, appPending2, appRejected,
            appTerminated, appRenewed);
        await db.SaveChangesAsync();

        LogPipeline(db, app1, t1, mgr, "Approved");
        LogPipeline(db, app2, t2, mgr, "Approved");
        LogPipeline(db, app3, t3, mgr, "Approved");
        LogPipeline(db, app4, t4, mgr, "Approved");
        LogPipeline(db, app5, t5, mgr, "Approved");
        LogPipeline(db, appScreenOk, t2, mgr, "Screening");
        LogPipeline(db, appScreenRej, t3, mgr, "Screening");
        LogPipeline(db, appPending1, t1, mgr, "Pending");
        LogPipeline(db, appPending2, t4, mgr, "Pending");
        LogPipeline(db, appRejected, t5, mgr, "Rejected");
        LogPipeline(db, appTerminated, t1, mgr, "Approved");
        LogPipeline(db, appRenewed, t3, mgr, "Approved");
        await db.SaveChangesAsync();

        // ── Documents (regular applications only) ────────────────────────────
        db.Documents.AddRange(
            Doc(app1, t1, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app1, t1, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app2, t2, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app2, t2, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app3, t3, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app3, t3, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app4, t4, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app4, t4, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app5, t5, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(app5, t5, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appScreenOk, t2, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appScreenOk, t2, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appScreenRej, t3, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appScreenRej, t3, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusRejected, "Income proof is unclear — upload a recent stamped salary certificate."),
            Doc(appPending1, t1, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appPending1, t1, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appPending2, t4, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appPending2, t4, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appRejected, t5, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appRejected, t5, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appTerminated, t1, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appTerminated, t1, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appRenewed, t3, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appRenewed, t3, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted));
        await db.SaveChangesAsync();

        // ── Leases ───────────────────────────────────────────────────────────
        var lease1 = Lease(app1, 220m, "Active");
        var lease2 = Lease(app2, 190m, "Active");
        var lease3 = Lease(app3, 290m, "Active");
        var lease4 = Lease(app4, 230m, "Active");
        var lease5 = Lease(app5, 380m, "Active");
        var lease6 = Lease(appTerminated, 360m, "Terminated");
        var lease7 = Lease(appRenewed, 300m, "Renewed");

        db.Leases.AddRange(lease1, lease2, lease3, lease4, lease5, lease6, lease7);
        await db.SaveChangesAsync();

        var renewStart = lease7.LeaseEndDate.AddDays(1);
        var appRenewBu101 = new LeaseApplication
        {
            UserId             = t3.UserId,
            UnitId             = units.Budaiya[0].UnitId,
            RequestedStartDate = renewStart,
            RequestedEndDate   = renewStart.AddMonths(9),
            Status             = "Screening",
            Notes              = "Renewal request for BU101 after renewed lease.",
            ParentLeaseId      = lease7.LeaseId,
            CreatedAt          = DateTime.Today.AddDays(-3)
        };
        db.LeaseApplications.Add(appRenewBu101);
        await db.SaveChangesAsync();

        LogPipeline(db, appRenewBu101, t3, mgr, "Screening", isRenewal: true);
        lease7.RenewLeaseApplicationId = appRenewBu101.ApplicationId;
        await db.SaveChangesAsync();

        AddLeaseLog(db, lease1, mgr, "Active", "Lease created upon approval.", lease1.CreatedAt);
        AddLeaseLog(db, lease2, mgr, "Active", "Lease created upon approval.", lease2.CreatedAt);
        AddLeaseLog(db, lease3, mgr, "Active", "Lease created upon approval.", lease3.CreatedAt);
        AddLeaseLog(db, lease4, mgr, "Active", "Lease created upon approval.", lease4.CreatedAt);
        AddLeaseLog(db, lease5, mgr, "Active", "Lease created upon approval.", lease5.CreatedAt);
        AddLeaseLog(db, lease6, mgr, "Active", "Lease created upon approval.", lease6.CreatedAt);
        AddLeaseLog(db, lease6, mgr, "Terminated", "Lease term ended.", lease6.LeaseEndDate);
        AddLeaseLog(db, lease7, mgr, "Active", "Lease created upon approval.", lease7.CreatedAt);
        AddLeaseLog(db, lease7, mgr, "Renewed", "Lease renewed.", lease7.LeaseEndDate.AddDays(-10));

        SeedPayments(db, lease1, 220m, 8, 1);
        SeedPayments(db, lease2, 190m, 5, 1);
        SeedPayments(db, lease3, 290m, 3, 1);
        SeedPayments(db, lease4, 230m, 2, 1);
        SeedPayments(db, lease5, 380m, 1, 1);
        SeedPayments(db, lease6, 360m, 12, 0);
        SeedPayments(db, lease7, 300m, 12, 0);
        await db.SaveChangesAsync();

        if (users.Murtadha != null)
            await SeedMurtadhaAsync(db, logger, users.Murtadha, mgr, units.JuffairProperty);

        db.Notifications.AddRange(
            Notif(t1.UserId,  "Your lease application for unit S101 has been approved. Welcome!", "LeaseUpdate"),
            Notif(t2.UserId,  "Your application for unit A102 is under screening.", "LeaseUpdate"),
            Notif(t3.UserId,  "Please re-upload your salary document for unit AD202.", "LeaseUpdate"),
            Notif(t5.UserId,  "Your application for unit C102 was rejected.", "LeaseUpdate"),
            Notif(mgr.UserId, "New screening application from Mohammed Al-Baker (A102).", "LeaseUpdate"),
            Notif(mgr.UserId, "Renewal application pending for unit BU101.", "LeaseUpdate"));
        await db.SaveChangesAsync();

        logger.LogInformation("[Seed] Lease applications, documents, and leases seeded ({Count} applications).",
            await db.LeaseApplications.CountAsync());
    }

    private static async Task SeedMurtadhaAsync(
        PropertyLeasingDbContext db,
        ILogger logger,
        User tenant,
        User mgr,
        Property juffair)
    {
        var leaseStart = DateTime.Today.AddMonths(-6);
        var leaseEnd   = DateTime.Today.AddDays(6);

        var mUnit = new Unit
        {
            PropertyId         = juffair.PropertyId,
            UnitNumber         = "J205",
            UnitType           = "2BR Apartment",
            Sizesqm            = 118,
            MonthlyRent        = 280m,
            AvailabilityStatus = "Occupied",
            Amenities          = "Sea View, Balcony, Parking x2, Gym, Central AC"
        };
        db.Units.Add(mUnit);
        await db.SaveChangesAsync();

        var appM = App(tenant.UserId, mUnit.UnitId, leaseStart, leaseEnd, "Approved", leaseStart.AddDays(-14));
        db.LeaseApplications.Add(appM);
        await db.SaveChangesAsync();

        LogPipeline(db, appM, tenant, mgr, "Approved");
        db.Documents.AddRange(
            Doc(appM, tenant, LeaseApplicationDocumentRules.NationalId,   LeaseApplicationDocumentRules.DocumentStatusSubmitted),
            Doc(appM, tenant, LeaseApplicationDocumentRules.SalaryIncome, LeaseApplicationDocumentRules.DocumentStatusSubmitted));
        await db.SaveChangesAsync();

        var leaseM = new Lease
        {
            ApplicationId   = appM.ApplicationId,
            LeaseStartDate  = leaseStart,
            LeaseEndDate    = leaseEnd,
            MonthlyRent     = 280m,
            SecurityDeposit = 560m,
            PaymentPlanType = "Cash",
            Status          = "Active",
            CreatedAt       = leaseStart
        };
        db.Leases.Add(leaseM);
        await db.SaveChangesAsync();

        AddLeaseLog(db, leaseM, mgr, "Active", "Lease activated (cash plan).", leaseStart);
        for (int m = 0; m < 6; m++)
        {
            db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = leaseM.LeaseId,
                AmountDue     = 280m,
                AmountPaid    = 280m,
                DueDate       = leaseStart.AddMonths(m),
                PaidDate      = leaseStart.AddMonths(m).AddDays(1),
                PaymentStatus = "Paid",
                Notes         = "Cash"
            });
        }

        db.Notifications.AddRange(
            Notif(tenant.UserId, $"Reminder: Your lease for unit J205 expires on {leaseEnd:dd MMM yyyy}.", "LeaseUpdate"),
            Notif(mgr.UserId,    $"Unit J205 lease expires in 6 days — schedule pre-tenancy maintenance if needed.", "LeaseUpdate"));
        await db.SaveChangesAsync();

        logger.LogInformation("[Seed] Murtadha demo lease (J205) created.");
    }

    private static LeaseApplication App(int userId, int unitId, DateTime start, DateTime end, string status, DateTime createdAt) =>
        new()
        {
            UserId             = userId,
            UnitId             = unitId,
            RequestedStartDate = start,
            RequestedEndDate   = end,
            Status             = status,
            CreatedAt          = createdAt
        };

    private static Lease Lease(LeaseApplication app, decimal rent, string status) =>
        new()
        {
            ApplicationId   = app.ApplicationId,
            LeaseStartDate  = app.RequestedStartDate!.Value,
            LeaseEndDate    = app.RequestedEndDate!.Value,
            MonthlyRent     = rent,
            SecurityDeposit = rent * 2,
            Status          = status,
            CreatedAt       = app.RequestedStartDate!.Value
        };

    private static Document Doc(
        LeaseApplication app,
        User tenant,
        string documentType,
        string status,
        string? rejectionReason = null)
    {
        var fileName = LeaseApplicationDocumentRules.BuildStoredFileName(
            app.ApplicationId, tenant.UserId, tenant.FullName, documentType);

        return new Document
        {
            ApplicationId   = app.ApplicationId,
            UserId          = tenant.UserId,
            FileName        = fileName,
            FileType        = documentType,
            StoragePath     = $"/seed/applications/{fileName}",
            Status          = status,
            RejectionReason = rejectionReason,
            UploadedAt      = app.CreatedAt.AddHours(2)
        };
    }

    private static void LogPipeline(
        PropertyLeasingDbContext db,
        LeaseApplication app,
        User tenant,
        User mgr,
        string finalStatus,
        bool isRenewal = false)
    {
        void Log(string status, int byUser, DateTime at) =>
            db.LeaseApplicationLogs.Add(new LeaseApplicationLog
            {
                ApplicationId   = app.ApplicationId,
                Status          = status,
                ChangedByUserId = byUser,
                CreatedAt       = at
            });

        Log("Pending", tenant.UserId, app.CreatedAt);

        if (finalStatus is "Pending")
            return;

        Log("Screening", mgr.UserId, app.CreatedAt.AddDays(isRenewal ? 1 : 2));

        if (finalStatus is "Screening")
            return;

        Log(finalStatus, mgr.UserId, app.CreatedAt.AddDays(isRenewal ? 2 : 5));
    }

    private static void AddLeaseLog(PropertyLeasingDbContext db, Lease lease, User mgr, string status, string? notes, DateTime at) =>
        db.LeaseLogs.Add(new LeaseLog
        {
            LeaseId         = lease.LeaseId,
            Status          = status,
            ChangedByUserId = mgr.UserId,
            Notes           = notes,
            CreatedAt       = at
        });

    private static void SeedPayments(PropertyLeasingDbContext db, Lease lease, decimal rent, int paidMonths, int pendingMonths)
    {
        for (int m = 0; m < paidMonths; m++)
        {
            db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = lease.LeaseId,
                AmountDue     = rent,
                AmountPaid    = rent,
                DueDate       = lease.LeaseStartDate.AddMonths(m),
                PaidDate      = lease.LeaseStartDate.AddMonths(m).AddDays(2),
                PaymentStatus = "Paid"
            });
        }

        for (int p = 0; p < pendingMonths; p++)
        {
            db.PaymentRecords.Add(new PaymentRecord
            {
                LeaseId       = lease.LeaseId,
                AmountDue     = rent,
                DueDate       = lease.LeaseStartDate.AddMonths(paidMonths + p),
                PaymentStatus = "Pending"
            });
        }
    }

    private static Notification Notif(int userId, string message, string type) =>
        new()
        {
            UserId           = userId,
            Message          = message,
            NotificationType = type,
            Status           = "Unread",
            CreatedAt        = DateTime.Now
        };
}
