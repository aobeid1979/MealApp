namespace MealApp.Services.Models;

public class SupportRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? UserId { get; set; }

    public bool IsAuthenticated { get; set; }

    public string? ImageFileName { get; set; }

    public string? ImageContentType { get; set; }

    public long? ImageSizeBytes { get; set; }

    public SupportRequestAttachment? Attachment { get; set; }
}
