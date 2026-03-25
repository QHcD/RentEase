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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── Seed roles and users on startup ──────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        await ContextSeed.SeedRolesAndUsersAsync(scope.ServiceProvider);
    }
    catch { }
}

app.Run();