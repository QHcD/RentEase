using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("PropertyImage")]
public class PropertyImage
{
    [Key]
    public int Id { get; set; }

    [Column("PropertyID")]
    public int PropertyId { get; set; }

    [Required]
    [StringLength(300)]
    public string ImagePath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [ForeignKey("PropertyId")]
    [InverseProperty("PropertyImages")]
    public virtual Property Property { get; set; } = null!;
}
