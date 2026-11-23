using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class ResendVerificationCodeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
