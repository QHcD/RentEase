using Microsoft.AspNetCore.Authentication.Cookies;
using PropertyLeasing.Reporting.Services;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:63199";

// ── MVC ───────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Session (to store JWT token between requests) ─────
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});

// ── Cookie Auth (for MVC login state) ─────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan   = TimeSpan.FromHours(1);
    });

// ── HttpClient pointing to the Web API ────────────────
// Development: accept localhost HTTPS dev certificate so chart APIs do not silently fail.
var relaxSslDev = builder.Environment.IsDevelopment();
builder.Services.AddHttpClient<ApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/'));
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        if (relaxSslDev)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        return handler;
    });

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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Reports}/{action=Occupancy}/{id?}");

app.Run();
