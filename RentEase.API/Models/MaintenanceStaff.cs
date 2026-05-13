using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("MaintenanceStaff")]
public class MaintenanceStaff
{
    [Key]
    [Column("StaffID")]
    public int StaffId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    // e.g. Plumbing / Electrical / HVAC / Carpentry / General
    [StringLength(200)]
    public string? SkillProfile { get; set; }

    // Available / Busy / OffDuty
    [StringLength(50)]
    public string AvailabilityStatus { get; set; } = "Available";

    [ForeignKey("UserId")]
    [InverseProperty("StaffProfile")]
    public virtual User User { get; set; } = null!;
}
