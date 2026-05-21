using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("LeaseRefund")]
public class LeaseRefund
{
    [Key]
    public int RefundId { get; set; }

    [Column("LeaseID")]
    public int LeaseId { get; set; }

    // Months that were consumed (including current month — forfeited)
    public int MonthsConsumed { get; set; }

    // Months that get refunded
    public int MonthsRefunded { get; set; }

    // Total amount that was paid (Paid records only)
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPaid { get; set; }

    // Overdue amount deducted from refund
    [Column(TypeName = "decimal(10,2)")]
    public decimal OverdueDeducted { get; set; }

    // Final refund amount = TotalPaid - (MonthsConsumed * MonthlyRent) - OverdueDeducted
    [Column(TypeName = "decimal(10,2)")]
    public decimal RefundAmount { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CancelledAt { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Notes { get; set; }

    [ForeignKey("LeaseId")]
    public virtual Lease Lease { get; set; } = null!;
}
