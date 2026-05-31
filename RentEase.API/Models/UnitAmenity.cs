using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("UnitAmenities")]
public class UnitAmenity
{
    [Column("UnitID")]
    public int UnitId { get; set; }

    [Column("AmenityID")]
    public int AmenityId { get; set; }

    [ForeignKey(nameof(UnitId))]
    [InverseProperty(nameof(Unit.UnitAmenities))]
    public virtual Unit Unit { get; set; } = null!;

    [ForeignKey(nameof(AmenityId))]
    [InverseProperty(nameof(Amenity.UnitAmenities))]
    public virtual Amenity Amenity { get; set; } = null!;
}
