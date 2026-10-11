namespace MealApp.Services.Models;

public class BrevoEmailOptions
{
    public bool Enabled { get; set; } = false;

    public string ApiKey { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    public string ToEmail { get; set; } = string.Empty;
}
