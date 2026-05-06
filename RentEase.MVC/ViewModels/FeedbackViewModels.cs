using System.ComponentModel.DataAnnotations;

namespace PropertyLeasing.MVC.ViewModels;

public class CreateFeedbackViewModel
{
    public int    UnitId       { get; set; }
    public string UnitNumber   { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int? Rating { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}
