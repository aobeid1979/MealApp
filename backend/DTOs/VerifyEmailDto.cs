using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class VerifyEmailDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "The code must be 6 digits.")]
    public string Code { get; set; } = string.Empty;
}
