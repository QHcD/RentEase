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

    // PendingPayment / Approved / Active / Terminated / Renewed
    [StringLength(50)]
    public string Status { get; set; } = "Active";

    // Full / Monthly
    [StringLength(20)]
    public string? PaymentPlanType { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // FK: renewal application for this lease (null for new leases, set when tenant requests renewal)
    [Column("RenewLeaseApplicationID")]
    public int? RenewLeaseApplicationId { get; set; }

    // FK: scheduled termination row (null = no termination scheduled; mutually exclusive with RenewLeaseApplicationId)
    [Column("TerminationID")]
    public int? TerminationId { get; set; }

    [ForeignKey("ApplicationId")]
    [InverseProperty("Leases")]
    public virtual LeaseApplication Application { get; set; } = null!;

    [ForeignKey("RenewLeaseApplicationId")]
    public virtual LeaseApplication? RenewLeaseApplication { get; set; }

    [ForeignKey("TerminationId")]
    public virtual Termination? Termination { get; set; }

    [InverseProperty("Lease")]
    public virtual ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();

    [InverseProperty("Lease")]
    public virtual ICollection<LeaseLog> LeaseLogs { get; set; } = new List<LeaseLog>();
}
