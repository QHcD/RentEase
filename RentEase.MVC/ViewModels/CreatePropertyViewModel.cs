using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PropertyLeasing.MVC.ViewModels;

public class CreatePropertyViewModel : IPropertyAmenitiesForm{
    /// <summary>Optional property images uploaded at creation time.</summary>
    public List<IFormFile>? Images { get; set; }
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

    [Display(Name = "Total property size (m²)")]
    [Range(typeof(decimal), "50", "1000000", ErrorMessage = "Total size must be between 50 and 1,000,000 m².")]
    public decimal TotalSizeSqm { get; set; } = 500m;

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

    /// <summary>Area (m²) for each unit on this floor, in numbering order.</summary>
    public List<decimal> UnitAreasSqm { get; set; } = new();
}
