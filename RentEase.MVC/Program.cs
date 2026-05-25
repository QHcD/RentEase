using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.MVC.Services;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core — App Database ────────────────────────────
builder.Services.AddDbContext<PropertyLeasingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── EF Core — Identity Database ───────────────────────
builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection")));

// ── ASP.NET Identity ──────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
})
.AddEntityFrameworkStores<AppIdentityDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

// Configure login path
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ── MVC ───────────────────────────────────────────────
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
    mvcBuilder.AddRazorRuntimeCompilation();

// ── HttpClient for API calls (Public Lookup page) ─────
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001");
});

// ── App Services ──────────────────────────────────────
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<LeaseApplicationDocumentService>();
builder.Services.AddHostedService<MaintenanceDailyService>();

var app = builder.Build();

// ── Middleware ────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.IsInRole("MaintenanceStaff"))
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isAllowed =
            path.StartsWith("/Maintenance", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Notifications", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Account/Profile", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            context.Response.Redirect("/Maintenance");
            return;
        }
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── Apply pending EF migrations then seed ────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                     .CreateLogger("Startup");
    try
    {
        // Business database: apply all outstanding migrations
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Business DB migrations applied.");
    }
    catch (Exception ex) { logger.LogError(ex, "Business DB migration failed."); }

    try
    {
        // Auto-add ImagePath column to MaintenanceRequest if it doesn't exist yet
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='MaintenanceRequest' AND COLUMN_NAME='ImagePath')
                ALTER TABLE MaintenanceRequest ADD ImagePath NVARCHAR(300) NULL;
        ");
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='MaintenanceRequest' AND COLUMN_NAME='ResolutionImagePath')
                ALTER TABLE MaintenanceRequest ADD ResolutionImagePath NVARCHAR(300) NULL;
        ");
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='MaintenanceRequest' AND COLUMN_NAME='ScheduledDate')
                ALTER TABLE MaintenanceRequest ADD ScheduledDate DATETIME NULL;
        ");
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='MaintenanceRequest' AND COLUMN_NAME='CancellationReason')
                ALTER TABLE MaintenanceRequest ADD CancellationReason NVARCHAR(200) NULL;
        ");
        logger.LogInformation("MaintenanceRequest extra columns ensured.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not auto-add ImagePath column (non-fatal)."); }

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Document' AND COLUMN_NAME = 'Status')
            BEGIN
                ALTER TABLE [Document] ADD [Status] NVARCHAR(50) NOT NULL
                    CONSTRAINT [DF_Document_Status] DEFAULT 'Submitted' WITH VALUES;
            END;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Document' AND COLUMN_NAME = 'RejectionReason')
                ALTER TABLE [Document] ADD [RejectionReason] NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260519120000_AddDocumentReviewStatus')
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260519120000_AddDocumentReviewStatus', N'9.0.0');
        ");
        logger.LogInformation("Document review columns ensured.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not ensure Document review columns (non-fatal)."); }

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Property' AND COLUMN_NAME = 'TotalSizeSqm')
                ALTER TABLE [Property] ADD [TotalSizeSqm] FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260523120000_AddPropertyTotalSizeSqm')
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260523120000_AddPropertyTotalSizeSqm', N'9.0.0');
        ");
        logger.LogInformation("Property TotalSizeSqm column ensured.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not ensure Property TotalSizeSqm column (non-fatal)."); }

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PropertyImage')
            CREATE TABLE [PropertyImage] (
                [Id]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [PropertyID] INT NOT NULL,
                [ImagePath]  NVARCHAR(300) NOT NULL,
                [SortOrder]  INT NOT NULL DEFAULT 0,
                CONSTRAINT [FK_PropertyImage_Property]
                    FOREIGN KEY ([PropertyID]) REFERENCES [Property]([PropertyID]) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='UnitImage')
            CREATE TABLE [UnitImage] (
                [Id]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [UnitID]    INT NOT NULL,
                [ImagePath] NVARCHAR(300) NOT NULL,
                [SortOrder] INT NOT NULL DEFAULT 0,
                CONSTRAINT [FK_UnitImage_Unit]
                    FOREIGN KEY ([UnitID]) REFERENCES [Unit]([UnitID]) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='LeaseRefund')
            CREATE TABLE [LeaseRefund] (
                [RefundId]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [LeaseID]         INT NOT NULL UNIQUE,
                [MonthsConsumed]  INT NOT NULL DEFAULT 0,
                [MonthsRefunded]  INT NOT NULL DEFAULT 0,
                [TotalPaid]       DECIMAL(10,2) NOT NULL DEFAULT 0,
                [OverdueDeducted] DECIMAL(10,2) NOT NULL DEFAULT 0,
                [RefundAmount]    DECIMAL(10,2) NOT NULL DEFAULT 0,
                [CancelledAt]     DATETIME NOT NULL DEFAULT GETDATE(),
                [Notes]           NVARCHAR(500) NULL,
                CONSTRAINT [FK_LeaseRefund_Lease]
                    FOREIGN KEY ([LeaseID]) REFERENCES [Lease]([LeaseID]) ON DELETE CASCADE
            );
        ");
        logger.LogInformation("PropertyImage, UnitImage and LeaseRefund tables ensured.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not ensure image tables (non-fatal)."); }

    try
    {
        // Replace any local /uploads/ property images with Unsplash CDN URLs (portable across machines)
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            -- Only run if ALL existing records are local paths (not CDN)
            IF EXISTS (SELECT 1 FROM [PropertyImage]) AND
               NOT EXISTS (SELECT 1 FROM [PropertyImage] WHERE [ImagePath] LIKE 'https://%')
            BEGIN
                DELETE FROM [PropertyImage];
            END;

            IF NOT EXISTS (SELECT 1 FROM [PropertyImage])
            BEGIN
                -- 10 properties × 3 images each, matched by property name
                INSERT INTO [PropertyImage] ([PropertyID], [ImagePath], [SortOrder])
                SELECT p.[PropertyID], img.[ImagePath], img.[SortOrder]
                FROM [Property] p
                CROSS APPLY (
                    SELECT v.ImagePath, v.SortOrder FROM (VALUES
                        -- Seef Tower Residences (Residential)
                        (N'Seef Tower Residences',    'https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?w=800&q=80', 0),
                        (N'Seef Tower Residences',    'https://images.unsplash.com/photo-1580587771525-78b9dba3b914?w=800&q=80', 1),
                        (N'Seef Tower Residences',    'https://images.unsplash.com/photo-1502005229762-cf1b2da7c5d6?w=800&q=80', 2),
                        -- Riffa Hills Apartments (Residential)
                        (N'Riffa Hills Apartments',   'https://images.unsplash.com/photo-1512917774080-9991f1c4c750?w=800&q=80', 0),
                        (N'Riffa Hills Apartments',   'https://images.unsplash.com/photo-1600585154526-990dced4db0d?w=800&q=80', 1),
                        (N'Riffa Hills Apartments',   'https://images.unsplash.com/photo-1464082354059-27db6ce50048?w=800&q=80', 2),
                        -- Juffair Bay Complex (Mixed)
                        (N'Juffair Bay Complex',      'https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?w=800&q=80', 0),
                        (N'Juffair Bay Complex',      'https://images.unsplash.com/photo-1486325212027-8081e485255e?w=800&q=80', 1),
                        (N'Juffair Bay Complex',      'https://images.unsplash.com/photo-1560518883-ce09059eeffa?w=800&q=80', 2),
                        -- Adliya Garden Residences (Residential)
                        (N'Adliya Garden Residences', 'https://images.unsplash.com/photo-1479839672679-a46483c0e7c8?w=800&q=80', 0),
                        (N'Adliya Garden Residences', 'https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?w=800&q=80', 1),
                        (N'Adliya Garden Residences', 'https://images.unsplash.com/photo-1448630360428-65456885c650?w=800&q=80', 2),
                        -- Amwaj Waterfront Living (Residential)
                        (N'Amwaj Waterfront Living',  'https://images.unsplash.com/photo-1580587771525-78b9dba3b914?w=800&q=80', 0),
                        (N'Amwaj Waterfront Living',  'https://images.unsplash.com/photo-1512917774080-9991f1c4c750?w=800&q=80', 1),
                        (N'Amwaj Waterfront Living',  'https://images.unsplash.com/photo-1502005229762-cf1b2da7c5d6?w=800&q=80', 2),
                        -- Budaiya Family Residences (Residential)
                        (N'Budaiya Family Residences','https://images.unsplash.com/photo-1464082354059-27db6ce50048?w=800&q=80', 0),
                        (N'Budaiya Family Residences','https://images.unsplash.com/photo-1600585154526-990dced4db0d?w=800&q=80', 1),
                        (N'Budaiya Family Residences','https://images.unsplash.com/photo-1479839672679-a46483c0e7c8?w=800&q=80', 2),
                        -- Diplomatic Quarter Suites (Mixed)
                        (N'Diplomatic Quarter Suites','https://images.unsplash.com/photo-1486325212027-8081e485255e?w=800&q=80', 0),
                        (N'Diplomatic Quarter Suites','https://images.unsplash.com/photo-1560518883-ce09059eeffa?w=800&q=80', 1),
                        (N'Diplomatic Quarter Suites','https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?w=800&q=80', 2),
                        -- Busaiteen Bay Apartments (Residential)
                        (N'Busaiteen Bay Apartments', 'https://images.unsplash.com/photo-1448630360428-65456885c650?w=800&q=80', 0),
                        (N'Busaiteen Bay Apartments', 'https://images.unsplash.com/photo-1580587771525-78b9dba3b914?w=800&q=80', 1),
                        (N'Busaiteen Bay Apartments', 'https://images.unsplash.com/photo-1502005229762-cf1b2da7c5d6?w=800&q=80', 2),
                        -- Tubli Business Park (Commercial)
                        (N'Tubli Business Park',      'https://images.unsplash.com/photo-1497366216548-37526070297c?w=800&q=80', 0),
                        (N'Tubli Business Park',      'https://images.unsplash.com/photo-1464082354059-27db6ce50048?w=800&q=80', 1),
                        (N'Tubli Business Park',      'https://images.unsplash.com/photo-1486325212027-8081e485255e?w=800&q=80', 2),
                        -- Muharraq Heritage Homes (Residential)
                        (N'Muharraq Heritage Homes',  'https://images.unsplash.com/photo-1512917774080-9991f1c4c750?w=800&q=80', 0),
                        (N'Muharraq Heritage Homes',  'https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?w=800&q=80', 1),
                        (N'Muharraq Heritage Homes',  'https://images.unsplash.com/photo-1600585154526-990dced4db0d?w=800&q=80', 2)
                    ) AS v(PropertyName, ImagePath, SortOrder)
                    WHERE v.PropertyName = p.[Name]
                ) img;
            END;
        ");
        logger.LogInformation("Property images seeded with CDN URLs.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not seed property images (non-fatal)."); }

    try
    {
        // Seed unit images — 3 local .jpg files per unit type, covers all 203 units.
        // Deletes any old/partial records first, then re-inserts for every unit.
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            -- Remove partial/old records so we can do a clean full insert
            DELETE FROM [UnitImage]
            WHERE [UnitID] IN (
                SELECT u.[UnitID] FROM [Unit] u
                WHERE u.[UnitType] IN (
                    'Studio','1BR Apartment','2BR Apartment','3BR Apartment',
                    '4BR Apartment','Office','Shop'
                )
            )
            AND [ImagePath] NOT LIKE '/uploads/units/%';

            -- Insert 3 images per unit only for units that still have none
            WITH TypeImages AS (
                SELECT UnitType, ImagePath, SortOrder FROM (VALUES
                    ('Studio',        '/uploads/units/studio/1.jpg', 0),
                    ('Studio',        '/uploads/units/studio/2.jpg', 1),
                    ('Studio',        '/uploads/units/studio/3.jpg', 2),
                    ('1BR Apartment', '/uploads/units/1br/1.jpg',    0),
                    ('1BR Apartment', '/uploads/units/1br/2.jpg',    1),
                    ('1BR Apartment', '/uploads/units/1br/3.jpg',    2),
                    ('2BR Apartment', '/uploads/units/2br/1.jpg',    0),
                    ('2BR Apartment', '/uploads/units/2br/2.jpg',    1),
                    ('2BR Apartment', '/uploads/units/2br/3.jpg',    2),
                    ('3BR Apartment', '/uploads/units/3br/1.jpg',    0),
                    ('3BR Apartment', '/uploads/units/3br/2.jpg',    1),
                    ('3BR Apartment', '/uploads/units/3br/3.jpg',    2),
                    ('4BR Apartment', '/uploads/units/4br/1.jpg',    0),
                    ('4BR Apartment', '/uploads/units/4br/2.jpg',    1),
                    ('4BR Apartment', '/uploads/units/4br/3.jpg',    2),
                    ('Office',        '/uploads/units/office/1.jpg', 0),
                    ('Office',        '/uploads/units/office/2.jpg', 1),
                    ('Office',        '/uploads/units/office/3.jpg', 2),
                    ('Shop',          '/uploads/units/shop/1.jpg',   0),
                    ('Shop',          '/uploads/units/shop/2.jpg',   1),
                    ('Shop',          '/uploads/units/shop/3.jpg',   2)
                ) AS T(UnitType, ImagePath, SortOrder)
            )
            INSERT INTO [UnitImage] ([UnitID], [ImagePath], [SortOrder])
            SELECT u.[UnitID], ti.[ImagePath], ti.[SortOrder]
            FROM [Unit] u
            INNER JOIN TypeImages ti ON ti.[UnitType] = u.[UnitType]
            WHERE NOT EXISTS (
                SELECT 1 FROM [UnitImage] ui
                WHERE ui.[UnitID] = u.[UnitID]
                  AND ui.[ImagePath] LIKE '/uploads/units/%'
            );
        ");
        logger.LogInformation("Unit images seeded (local .jpg).");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not seed unit images (non-fatal)."); }

    try
    {
        // Create MaintenanceRequestLog table if it does not exist yet
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = 'MaintenanceRequestLog'
            )
            CREATE TABLE [MaintenanceRequestLog] (
                [LogID]             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [RequestID]         INT NOT NULL,
                [Action]            NVARCHAR(100) NOT NULL,
                [Details]           NVARCHAR(500) NULL,
                [PerformedByUserID] INT NULL,
                [PerformedAt]       DATETIME NOT NULL CONSTRAINT [DF_MaintenanceRequestLog_PerformedAt] DEFAULT GETDATE(),
                CONSTRAINT [FK_MaintenanceRequestLog_Request]
                    FOREIGN KEY ([RequestID]) REFERENCES [MaintenanceRequest]([RequestID])
            );
        ");
        logger.LogInformation("MaintenanceRequestLog table ensured.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not create MaintenanceRequestLog table (non-fatal)."); }

    try
    {
        // Identity database: no migrations exist, so EnsureCreated creates
        // the schema on first run (no-op on an existing database).
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Database.EnsureCreatedAsync();
        logger.LogInformation("Identity DB ready.");
    }
    catch (Exception ex) { logger.LogError(ex, "Identity DB setup failed."); }

    try
    {
        // EF maps AppUser.Username → [DisplayUsername] column.
        // EnsureCreatedAsync won't add it to an existing DB, so we guard it manually.
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                          WHERE TABLE_NAME='AspNetUsers' AND COLUMN_NAME='DisplayUsername')
                ALTER TABLE [AspNetUsers] ADD DisplayUsername NVARCHAR(50) NULL;
        ");
        logger.LogInformation("AspNetUsers.DisplayUsername column ensured.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not ensure DisplayUsername column (non-fatal)."); }

    try
    {
        // Backfill serial phone numbers for all seeded users in Identity DB (AspNetUsers).
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Database.ExecuteSqlRawAsync(@"
            UPDATE [AspNetUsers] SET [Phone] = '66600001' WHERE [Email] = 'manager@propleasing.com'  AND ([Phone] IS NULL OR [Phone] != '66600001');
            UPDATE [AspNetUsers] SET [Phone] = '66600002' WHERE [Email] = 'tenant1@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600002');
            UPDATE [AspNetUsers] SET [Phone] = '66600003' WHERE [Email] = 'tenant2@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600003');
            UPDATE [AspNetUsers] SET [Phone] = '66600004' WHERE [Email] = 'tenant3@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600004');
            UPDATE [AspNetUsers] SET [Phone] = '66600005' WHERE [Email] = 'tenant4@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600005');
            UPDATE [AspNetUsers] SET [Phone] = '66600006' WHERE [Email] = 'tenant5@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600006');
            UPDATE [AspNetUsers] SET [Phone] = '66600007' WHERE [Email] = 'staff1@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600007');
            UPDATE [AspNetUsers] SET [Phone] = '66600008' WHERE [Email] = 'staff2@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600008');
            UPDATE [AspNetUsers] SET [Phone] = '66600009' WHERE [Email] = 'staff3@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600009');
            UPDATE [AspNetUsers] SET [Phone] = '66600010' WHERE [Email] = 'staff4@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600010');
            UPDATE [AspNetUsers] SET [Phone] = '66600011' WHERE [Email] = 'staff5@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600011');
            UPDATE [AspNetUsers] SET [Phone] = '66600012' WHERE [Email] = 'staff6@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600012');
            UPDATE [AspNetUsers] SET [Phone] = '66600013' WHERE [Email] = 'staff7@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600013');
            UPDATE [AspNetUsers] SET [Phone] = '66600014' WHERE [Email] = 'staff8@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600014');
            UPDATE [AspNetUsers] SET [Phone] = '66600015' WHERE [Email] = 'staff9@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600015');
            UPDATE [AspNetUsers] SET [Phone] = '66600016' WHERE [Email] = 'staff10@propleasing.com'  AND ([Phone] IS NULL OR [Phone] != '66600016');
        ");
        logger.LogInformation("Seeded user phone numbers backfilled in Identity DB.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not backfill phone numbers in Identity DB (non-fatal)."); }

    try
    {
        // Backfill serial phone numbers for all seeded users in business DB ([User] table).
        var db = scope.ServiceProvider.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE [User] SET [Phone] = '66600001' WHERE [Email] = 'manager@propleasing.com'  AND ([Phone] IS NULL OR [Phone] != '66600001');
            UPDATE [User] SET [Phone] = '66600002' WHERE [Email] = 'tenant1@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600002');
            UPDATE [User] SET [Phone] = '66600003' WHERE [Email] = 'tenant2@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600003');
            UPDATE [User] SET [Phone] = '66600004' WHERE [Email] = 'tenant3@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600004');
            UPDATE [User] SET [Phone] = '66600005' WHERE [Email] = 'tenant4@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600005');
            UPDATE [User] SET [Phone] = '66600006' WHERE [Email] = 'tenant5@example.com'      AND ([Phone] IS NULL OR [Phone] != '66600006');
            UPDATE [User] SET [Phone] = '66600007' WHERE [Email] = 'staff1@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600007');
            UPDATE [User] SET [Phone] = '66600008' WHERE [Email] = 'staff2@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600008');
            UPDATE [User] SET [Phone] = '66600009' WHERE [Email] = 'staff3@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600009');
            UPDATE [User] SET [Phone] = '66600010' WHERE [Email] = 'staff4@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600010');
            UPDATE [User] SET [Phone] = '66600011' WHERE [Email] = 'staff5@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600011');
            UPDATE [User] SET [Phone] = '66600012' WHERE [Email] = 'staff6@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600012');
            UPDATE [User] SET [Phone] = '66600013' WHERE [Email] = 'staff7@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600013');
            UPDATE [User] SET [Phone] = '66600014' WHERE [Email] = 'staff8@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600014');
            UPDATE [User] SET [Phone] = '66600015' WHERE [Email] = 'staff9@propleasing.com'   AND ([Phone] IS NULL OR [Phone] != '66600015');
            UPDATE [User] SET [Phone] = '66600016' WHERE [Email] = 'staff10@propleasing.com'  AND ([Phone] IS NULL OR [Phone] != '66600016');
        ");
        logger.LogInformation("Seeded user phone numbers backfilled in business DB.");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Could not backfill phone numbers in business DB (non-fatal)."); }

    try
    {
        await ContextSeed.SeedRolesAndUsersAsync(scope.ServiceProvider);
        logger.LogInformation("Seed completed.");
    }
    catch (Exception ex) { logger.LogError(ex, "Seed failed."); }
}

app.Run();