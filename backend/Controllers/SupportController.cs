using MealApp.DTOs;
using MealApp.Models;
using MealApp.Services;
using MealApp.Services.Models;
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
    private readonly ISupportRequestService _supportRequestService;

    public SupportController(
        UserManager<ApplicationUser> userManager,
        ISupportRequestService supportRequestService)
    {
        _userManager = userManager;
        _supportRequestService = supportRequestService;
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

        try
        {
            await _supportRequestService.ReceiveAsync(new SupportRequest
            {
                Title = requestDto.Title.Trim(),
                Description = requestDto.Description.Trim(),
                Name = user?.Name ?? requestDto.Name?.Trim(),
                Email = user?.Email ?? requestDto.Email?.Trim(),
                Phone = user is null ? requestDto.Phone?.Trim() : null,
                UserId = user?.Id,
                IsAuthenticated = user is not null,
                ImageFileName = requestDto.Image?.FileName,
                ImageContentType = requestDto.Image?.ContentType,
                ImageSizeBytes = requestDto.Image?.Length,
                Attachment = await CreateAttachmentAsync(requestDto.Image)
            });
        }
        catch (SupportRequestEmailException)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ApiResponse
                {
                    IsSuccess = false,
                    Result = null,
                    ErrorMessages = ["Support request could not be sent. Please try again later."],
                    StatusCode = StatusCodes.Status500InternalServerError
                });
        }

        return Ok(new
        {
            Message = "Support request received successfully."
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

    private static async Task<SupportRequestAttachment?> CreateAttachmentAsync(IFormFile? image)
    {
        if (image is null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);

        return new SupportRequestAttachment
        {
            FileName = Path.GetFileName(image.FileName),
            ContentType = image.ContentType,
            Content = memoryStream.ToArray()
        };
    }
}
