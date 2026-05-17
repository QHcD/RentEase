namespace PropertyLeasing.MVC.ViewModels;

/// <summary>Shared shape for Create / Edit property amenity UI.</summary>
public interface IPropertyAmenitiesForm
{
    List<string> SelectedFixedAmenities { get; set; }
    List<string> CustomAmenities { get; set; }
}
