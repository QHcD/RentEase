using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

public class CreatePropertyViewModel : IPropertyAmenitiesForm{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [Required]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [StringLength(50)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? PropertyType { get; set; }

    [Display(Name = "Number of floors")]
    [Range(1, 99)]
    public int NumberOfFloors { get; set; } = 1;

    [Display(Name = "Unit prefix (optional)")]
    [StringLength(20)]
    public string? UnitNumberPrefix { get; set; }

    /// <summary>Fixed amenity labels toggled on the form (must match server-defined options).</summary>
    public List<string> SelectedFixedAmenities { get; set; } = new();

    /// <summary>Manager-defined extras (each row may be deleted on the client).</summary>
    public List<string> CustomAmenities { get; set; } = new();

    public List<FloorUnitRowInput> FloorRows { get; set; } = new();
}

public class FloorUnitRowInput
{
    [Display(Name = "Units on this floor")]
    [Range(1, 99)]
    public int UnitsOnFloor { get; set; } = 1;
}
