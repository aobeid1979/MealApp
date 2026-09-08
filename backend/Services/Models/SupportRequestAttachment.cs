namespace MealApp.Services.Models;

public class SupportRequestAttachment
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];
}
