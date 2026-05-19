using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Data;

// Custom Identity User
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Display handle (separate from login email).</summary>
    [Column("DisplayUsername")]
    public string? Username { get; set; }

    public string? Phone { get; set; }
}

// Identity DbContext
public class AppIdentityDbContext : IdentityDbContext<AppUser>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
        : base(options) { }
}
