using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateUnitDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string ShortName { get; set; } = string.Empty;
}
