using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Models;
using PropertyLeasing.MVC.Services;
using PropertyLeasing.MVC.ViewModels;

namespace PropertyLeasing.MVC.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser>     _userManager;
    private readonly SignInManager<AppUser>   _signInManager;
    private readonly PropertyLeasingDbContext _db;
    private readonly EmailService             _emailService;

    public AccountController(
        UserManager<AppUser>     userManager,
        SignInManager<AppUser>   signInManager,
        PropertyLeasingDbContext  db,
        EmailService             emailService)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _db            = db;
        _emailService  = emailService;
    }

    private async Task<IActionResult> RedirectAfterLoginAsync(AppUser identityUser, string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        var roles = await _userManager.GetRolesAsync(identityUser);
        if (roles.Contains("MaintenanceStaff"))
            return RedirectToAction("Index", "Maintenance");

        return RedirectToAction("Index", "Home");
    }

    // GET /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("MaintenanceStaff"))
                return RedirectToAction("Index", "Maintenance");

            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Log the login action
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Login succeeded but account mapping is missing.");
                return View(model);
            }

            var appUser = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == user.Id);
            if (appUser != null)
            {
                _db.Logs.Add(new Log
                {
                    UserId    = appUser.UserId,
                    Action    = "Login",
                    Details   = $"User {model.Email} logged in",
                    LogLevel  = "Info",
                    Source    = "Web",
                    CreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            return await RedirectAfterLoginAsync(user, returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    // GET /Account/Register
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View();
    }

    // POST /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var identityUser = new AppUser
        {
            UserName       = model.Email,
            Email          = model.Email,
            FullName       = model.FullName,
            Phone          = model.Phone,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(identityUser, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(identityUser, "Tenant");

        // Create matching User record in app DB
        var appUser = new User
        {
            FullName       = model.FullName,
            Email          = model.Email,
            Phone          = model.Phone,
            Role           = "Tenant",
            IdentityUserId = identityUser.Id
        };
        _db.Users.Add(appUser);
        await _db.SaveChangesAsync();

        await _signInManager.SignInAsync(identityUser, isPersistent: false);
        TempData["Success"] = "Registration successful! Welcome to Property Leasing.";
        return await RedirectAfterLoginAsync(identityUser);
    }

    // POST /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // GET /Account/Profile
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(identityUser);

        var model = new ProfileViewModel
        {
            FullName = identityUser.FullName,
            Email    = identityUser.Email!,
            Phone    = identityUser.Phone,
            Role     = roles.FirstOrDefault() ?? "Tenant"
        };
        return View(model);
    }

    // POST /Account/Profile
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null) return NotFound();

        // Update name and phone
        identityUser.FullName = model.FullName;
        identityUser.Phone    = model.Phone;
        await _userManager.UpdateAsync(identityUser);

        // Also update app User table
        var appUser = await _db.Users.FirstOrDefaultAsync(u => u.IdentityUserId == identityUser.Id);
        if (appUser != null)
        {
            appUser.FullName = model.FullName;
            appUser.Phone    = model.Phone;
            await _db.SaveChangesAsync();
        }

        // Change password if provided
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var passResult = await _userManager.ChangePasswordAsync(
                identityUser, model.CurrentPassword!, model.NewPassword);
            if (!passResult.Succeeded)
            {
                foreach (var error in passResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }
        }

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction("Profile");
    }

    // GET /Account/ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    // POST /Account/ForgotPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var identityUser = await _userManager.FindByEmailAsync(model.Email);
        if (identityUser == null)
        {
            ModelState.AddModelError(string.Empty, "Account not found in our system.");
            return View(model);
        }

        // Invalidate previous codes for this email
        var oldCodes = _db.PasswordResetCodes
            .Where(c => c.Email == model.Email && !c.IsUsed && c.ExpiresAt > DateTime.Now);
        foreach (var c in oldCodes) c.IsUsed = true;

        var code = new Random().Next(100000, 999999).ToString();
        _db.PasswordResetCodes.Add(new PropertyLeasing.API.Models.PasswordResetCode
        {
            Email     = model.Email,
            Code      = code,
            ExpiresAt = DateTime.Now.AddMinutes(15),
            IsUsed    = false,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();

        // Send reset code via email
        try
        {
            await _emailService.SendPasswordResetAsync(
                toEmail: model.Email,
                toName:  identityUser.FullName ?? model.Email,
                code:    code);

            TempData["ResetEmail"]   = model.Email;
            TempData["EmailSent"]    = true;
        }
        catch
        {
            // Fallback: show code on screen if email fails
            TempData["ResetCode"]  = code;
            TempData["ResetEmail"] = model.Email;
        }

        return RedirectToAction("ResetPassword");
    }

    // GET /Account/ResetPassword
    [HttpGet]
    public IActionResult ResetPassword()
    {
        var email = TempData.Peek("ResetEmail") as string;
        if (string.IsNullOrEmpty(email))
            return RedirectToAction("ForgotPassword");

        var model = new VerifyResetCodeViewModel { Email = email };
        return View(model);
    }

    // POST /Account/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(VerifyResetCodeViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var record = await _db.PasswordResetCodes
            .Where(c => c.Email == model.Email
                     && c.Code  == model.Code
                     && !c.IsUsed
                     && c.ExpiresAt > DateTime.Now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (record == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired reset code. Please request a new one.");
            return View(model);
        }

        var identityUser = await _userManager.FindByEmailAsync(model.Email);
        if (identityUser == null)
        {
            ModelState.AddModelError(string.Empty, "Account not found.");
            return View(model);
        }

        var token  = await _userManager.GeneratePasswordResetTokenAsync(identityUser);
        var result = await _userManager.ResetPasswordAsync(identityUser, token, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        record.IsUsed = true;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Password reset successfully. You can now log in with your new password.";
        return RedirectToAction("Login");
    }

    // GET /Account/AccessDenied
    public IActionResult AccessDenied() => View();
}
