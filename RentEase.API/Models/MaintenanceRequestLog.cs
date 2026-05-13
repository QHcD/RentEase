using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("MaintenanceRequestLog")]
public class MaintenanceRequestLog
{
    [Key]
    [Column("LogID")]
    public int LogId { get; set; }

    [Column("RequestID")]
    public int RequestId { get; set; }

    // e.g. Submitted / Assigned / StatusChanged / Resolved / Closed
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = null!;

    [StringLength(500)]
    public string? Details { get; set; }

    [Column("PerformedByUserID")]
    public int? PerformedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PerformedAt { get; set; } = DateTime.Now;

    [ForeignKey("RequestId")]
    [InverseProperty("RequestLogs")]
    public virtual MaintenanceRequest MaintenanceRequest { get; set; } = null!;
}
