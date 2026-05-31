using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("Amenity")]
public class Amenity
{
    [Key]
    [Column("AmenityID")]
    public int AmenityId { get; set; }

    [Required]
    [StringLength(80)]
    public string Name { get; set; } = null!;

    [InverseProperty(nameof(PropertyAmenity.Amenity))]
    public virtual ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();

    [InverseProperty(nameof(UnitAmenity.Amenity))]
    public virtual ICollection<UnitAmenity> UnitAmenities { get; set; } = new List<UnitAmenity>();
}
