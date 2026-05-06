using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("LeaseLog")]
public partial class LeaseLog
{
    [Key]
    [Column("LogID")]
    public int LogId { get; set; }

    [Column("LeaseID")]
    public int LeaseId { get; set; }

    // Approved / Active / Renewal / Terminated / Expired
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = null!;

    [Column("ChangedByUserID")]
    public int ChangedByUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("LeaseId")]
    [InverseProperty("LeaseLogs")]
    public virtual Lease Lease { get; set; } = null!;

    [ForeignKey("ChangedByUserId")]
    [InverseProperty("LeaseLogs")]
    public virtual User ChangedByUser { get; set; } = null!;
}
