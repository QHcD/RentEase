using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("User")]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [StringLength(20)]
    public string? Phone { get; set; }

    // Tenant / PropertyManager / MaintenanceStaff
    [StringLength(50)]
    public string Role { get; set; } = "Tenant";

    // For MaintenanceStaff: Plumbing, Electrical, HVAC, General
    [StringLength(200)]
    public string? SkillProfile { get; set; }

    // For MaintenanceStaff: Available / Busy / OffDuty
    [StringLength(50)]
    public string? AvailabilityStatus { get; set; }

    // Links to ASP.NET Identity user
    [StringLength(450)]
    public string? IdentityUserId { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<LeaseApplication> LeaseApplications { get; set; } = new List<LeaseApplication>();

    [InverseProperty("Tenant")]
    public virtual ICollection<MaintenanceRequest> MaintenanceRequestsAsTenant { get; set; } = new List<MaintenanceRequest>();

    [InverseProperty("AssignedStaff")]
    public virtual ICollection<MaintenanceRequest> MaintenanceRequestsAsStaff { get; set; } = new List<MaintenanceRequest>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("User")]
    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    [InverseProperty("User")]
    public virtual ICollection<Log> Logs { get; set; } = new List<Log>();

    [InverseProperty("User")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
