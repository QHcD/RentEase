using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PropertyLeasing.Reporting.Services;
using PropertyLeasing.Reporting.ViewModels;
using System.Security.Claims;

namespace PropertyLeasing.Reporting.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api)
    {
        _api = api;
    }

    // GET /Account/Login
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Reports");
        return View();
    }

    // POST /Account/Login
    // Authenticates against the Web API using JWT, then stores token in session
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Call the API to get JWT token
        var authResult = await _api.LoginAsync(model.Email, model.Password);

        if (authResult == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials or only Property Managers can access this tool.");
            return View(model);
        }

        // Only Property Managers can use the Reporting App
        if (authResult.Role != "PropertyManager")
        {
            ModelState.AddModelError(string.Empty, "Access denied. Only Property Managers can use the Reporting App.");
            return View(model);
        }

        // Store JWT token in session for subsequent API calls
        HttpContext.Session.SetString("JwtToken", authResult.Token);
        HttpContext.Session.SetString("UserFullName", authResult.FullName);

        // Sign in with cookie auth for the MVC app itself
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name,  authResult.Email),
            new Claim(ClaimTypes.Email, authResult.Email),
            new Claim(ClaimTypes.Role,  authResult.Role),
            new Claim("FullName",       authResult.FullName),
            new Claim("JwtToken",       authResult.Token)
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction("Index", "Reports");
    }

    // POST /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();
}
