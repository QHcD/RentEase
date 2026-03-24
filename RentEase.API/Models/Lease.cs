using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("Lease")]
public partial class Lease
{
    [Key]
    [Column("LeaseID")]
    public int LeaseId { get; set; }

    [Column("ApplicationID")]
    public int ApplicationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime LeaseStartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime LeaseEndDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MonthlyRent { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal SecurityDeposit { get; set; }

    // Active / Expired / Terminated / Renewed
    [StringLength(50)]
    public string Status { get; set; } = "Active";

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("ApplicationId")]
    [InverseProperty("Leases")]
    public virtual LeaseApplication Application { get; set; } = null!;

    [InverseProperty("Lease")]
    public virtual ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
}
