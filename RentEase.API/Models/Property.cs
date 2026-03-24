using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("Property")]
public partial class Property
{
    [Key]
    [Column("PropertyID")]
    public int PropertyId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(250)]
    public string? Description { get; set; }

    [Required]
    [StringLength(200)]
    public string Address { get; set; } = null!;

    [StringLength(50)]
    public string? City { get; set; }

    // Residential / Commercial
    [StringLength(50)]
    public string? PropertyType { get; set; }

    [StringLength(100)]
    public string? ImgPath { get; set; }

    [InverseProperty("Property")]
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
