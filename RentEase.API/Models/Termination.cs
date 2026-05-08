using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("Termination")]
public class Termination
{
    [Key]
    public int TerminationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime TerminationDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
