using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class SupportRequestDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public IFormFile? Image { get; set; }
}
