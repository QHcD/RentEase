using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("LeaseApplicationLog")]
public partial class LeaseApplicationLog
{
    [Key]
    [Column("LogID")]
    public int LogId { get; set; }

    [Column("ApplicationID")]
    public int ApplicationId { get; set; }

    // Pending / Screening / Approved / Rejected
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = null!;

    [Column("ChangedByUserID")]
    public int ChangedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("ApplicationId")]
    [InverseProperty("ApplicationLogs")]
    public virtual LeaseApplication Application { get; set; } = null!;

    [ForeignKey("ChangedByUserId")]
    [InverseProperty("LeaseApplicationLogs")]
    public virtual User ChangedByUser { get; set; } = null!;
}
