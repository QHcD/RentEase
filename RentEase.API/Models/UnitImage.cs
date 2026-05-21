using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("UnitImage")]
public class UnitImage
{
    [Key]
    public int Id { get; set; }

    [Column("UnitID")]
    public int UnitId { get; set; }

    [Required]
    [StringLength(300)]
    public string ImagePath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [ForeignKey("UnitId")]
    [InverseProperty("UnitImages")]
    public virtual Unit Unit { get; set; } = null!;
}
