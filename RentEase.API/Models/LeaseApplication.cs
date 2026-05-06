using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("LeaseApplication")]
public partial class LeaseApplication
{
    [Key]
    [Column("ApplicationID")]
    public int ApplicationId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Column("UnitID")]
    public int UnitId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RequestedStartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RequestedEndDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Pending / Screening / Approved / Rejected
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("UnitId")]
    [InverseProperty("LeaseApplications")]
    public virtual Unit Unit { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("LeaseApplications")]
    public virtual User User { get; set; } = null!;

    [InverseProperty("Application")]
    public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();

    [InverseProperty("Application")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    [InverseProperty("Application")]
    public virtual ICollection<LeaseApplicationLog> ApplicationLogs { get; set; } = new List<LeaseApplicationLog>();
}
