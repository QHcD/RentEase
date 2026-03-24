using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.DTOs;
using PropertyLeasing.API.Services;

namespace PropertyLeasing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser>  _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly JwtService            _jwtService;

    public AuthController(
        UserManager<AppUser>  userManager,
        SignInManager<AppUser> signInManager,
        JwtService            jwtService)
    {
        _userManager  = userManager;
        _signInManager = signInManager;
        _jwtService   = jwtService;
    }

    // POST api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token    = token,
            Email    = user.Email!,
            FullName = user.FullName,
            Role     = roles.FirstOrDefault() ?? "Tenant",
            Expiry   = DateTime.UtcNow.AddMinutes(60)
        });
    }

    // POST api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing != null)
            return BadRequest(new { message = "Email already registered." });

        var user = new AppUser
        {
            UserName       = dto.Email,
            Email          = dto.Email,
            FullName       = dto.FullName,
            Phone          = dto.Phone,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // All self-registered users are Tenants
        await _userManager.AddToRoleAsync(user, "Tenant");

        return Ok(new { message = "Registration successful. You can now log in." });
    }
}
