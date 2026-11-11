using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Security.Claims;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class SupportController : ApiControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly UserManager<ApplicationUser> _userManager;

    public SupportController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] SupportRequestDto requestDto)
    {
        var user = await GetAuthenticatedUserAsync();

        if (User.Identity?.IsAuthenticated == true && user is null)
        {
            return BadRequest(new
            {
                Message = "Authenticated user could not be found."
            });
        }

        var validationErrors = ValidateRequest(requestDto, user is not null);

        if (validationErrors.Count > 0)
        {
            return BadRequest(new
            {
                Message = "Support request validation failed.",
                Errors = validationErrors
            });
        }

        // Delivery is unavailable. Do not acknowledge or store a request as sent.
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiResponse
        {
            IsSuccess = false,
            Result = null,
            ErrorMessages = ["Support email delivery is temporarily unavailable. Please try again later."],
            StatusCode = StatusCodes.Status503ServiceUnavailable
        });
    }

    private async Task<ApplicationUser?> GetAuthenticatedUserAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _userManager.FindByIdAsync(userId);
    }

    private static List<string> ValidateRequest(SupportRequestDto requestDto, bool isAuthenticated)
    {
        var errors = new List<string>();
        var email = requestDto.Email?.Trim();
        var phone = requestDto.Phone?.Trim();

        if (string.IsNullOrWhiteSpace(requestDto.Title))
        {
            errors.Add("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(requestDto.Description))
        {
            errors.Add("Description is required.");
        }

        if (requestDto.Image is not null)
        {
            if (requestDto.Image.Length > MaxImageSizeBytes)
            {
                errors.Add("Image size cannot be greater than 5 MB.");
            }

            var extension = Path.GetExtension(requestDto.Image.FileName);

            if (!AllowedImageContentTypes.Contains(requestDto.Image.ContentType)
                || !AllowedImageExtensions.Contains(extension))
            {
                errors.Add("Image must be a JPG, PNG, or WEBP file.");
            }
        }

        if (!isAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            {
                errors.Add("Email or phone number is required.");
            }

            if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            {
                errors.Add("Enter a valid email address.");
            }
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);

            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
