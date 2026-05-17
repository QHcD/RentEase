using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

public class EditPropertyViewModel : IPropertyAmenitiesForm
{
    public int PropertyId { get; set; }

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

    [StringLength(100)]
    public string? ImgPath { get; set; }

    [Display(Name = "Grace period (days)")]
    [Range(0, 365)]
    public int GracePeriodDays { get; set; } = 5;

    [Display(Name = "Late fee (%)")]
    [Range(0, 100)]
    public decimal LateFeePercent { get; set; } = 5;

    public List<string> SelectedFixedAmenities { get; set; } = new();
    public List<string> CustomAmenities { get; set; } = new();
}
