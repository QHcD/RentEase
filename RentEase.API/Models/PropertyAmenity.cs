using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("PropertyAmenities")]
public class PropertyAmenity
{
    [Column("PropertyID")]
    public int PropertyId { get; set; }

    [Column("AmenityID")]
    public int AmenityId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    [InverseProperty(nameof(Property.PropertyAmenities))]
    public virtual Property Property { get; set; } = null!;

    [ForeignKey(nameof(AmenityId))]
    [InverseProperty(nameof(Amenity.PropertyAmenities))]
    public virtual Amenity Amenity { get; set; } = null!;
}
