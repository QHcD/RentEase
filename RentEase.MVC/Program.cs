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
.AddDefaultTokenProviders();

// Configure login path
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ── MVC ───────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── HttpClient for API calls (Public Lookup page) ─────
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001");
});

// ── App Services ──────────────────────────────────────
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<EmailService>();
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
        await ContextSeed.SeedRolesAndUsersAsync(scope.ServiceProvider);
        logger.LogInformation("Seed completed.");
    }
    catch (Exception ex) { logger.LogError(ex, "Seed failed."); }
}

app.Run();