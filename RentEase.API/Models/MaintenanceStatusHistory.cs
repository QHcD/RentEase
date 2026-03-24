using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("MaintenanceStatusHistory")]
public partial class MaintenanceStatusHistory
{
    [Key]
    [Column("HistoryID")]
    public int HistoryId { get; set; }

    [Column("RequestID")]
    public int RequestId { get; set; }

    [StringLength(50)]
    public string? OldStatus { get; set; }

    [StringLength(50)]
    public string NewStatus { get; set; } = null!;

    [StringLength(250)]
    public string? Notes { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ChangedAt { get; set; } = DateTime.Now;

    [Column("ChangedByUserID")]
    public int? ChangedByUserId { get; set; }

    [ForeignKey("RequestId")]
    [InverseProperty("StatusHistory")]
    public virtual MaintenanceRequest MaintenanceRequest { get; set; } = null!;
}
