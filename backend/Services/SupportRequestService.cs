using MealApp.Services.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json.Serialization;

namespace MealApp.Services;

public class SupportRequestService : ISupportRequestService
{
    private const string BrevoTransactionalEmailUrl = "https://api.brevo.com/v3/smtp/email";

    private readonly HttpClient _httpClient;
    private readonly BrevoEmailOptions _options;
    private readonly ILogger<SupportRequestService> _logger;

    public SupportRequestService(
        HttpClient httpClient,
        IOptions<BrevoEmailOptions> options,
        ILogger<SupportRequestService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ReceiveAsync(SupportRequest supportRequest)
    {
        ValidateOptions();

        var payload = CreatePayload(supportRequest);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoTransactionalEmailUrl);
            request.Headers.Add("api-key", _options.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogError(
                    "Brevo support email request failed with status {StatusCode}. Response: {ResponseBody}",
                    response.StatusCode,
                    responseBody);

                throw new SupportRequestEmailException(
                    "Support request could not be sent.");
            }
        }
        catch (Exception exception)
        {
            if (exception is SupportRequestEmailException)
            {
                throw;
            }

            _logger.LogError(exception, "Failed to send support request email through Brevo.");

            throw new SupportRequestEmailException(
                "Support request could not be sent.",
                exception);
        }
    }

    private BrevoEmailPayload CreatePayload(SupportRequest supportRequest)
    {
        var payload = new BrevoEmailPayload
        {
            Sender = new BrevoEmailContact(_options.SenderEmail, _options.SenderName),
            To = [new BrevoEmailContact(_options.ToEmail)],
            Subject = $"[MealList Support] {supportRequest.Title}",
            TextContent = CreateTextBody(supportRequest)
        };

        if (!string.IsNullOrWhiteSpace(supportRequest.Email) && IsValidEmail(supportRequest.Email))
        {
            payload.ReplyTo = new BrevoEmailContact(
                supportRequest.Email,
                string.IsNullOrWhiteSpace(supportRequest.Name) ? null : supportRequest.Name);
        }

        if (supportRequest.Attachment is not null)
        {
            payload.Attachment =
            [
                new BrevoEmailAttachment(
                    supportRequest.Attachment.FileName,
                    Convert.ToBase64String(supportRequest.Attachment.Content))
            ];
        }

        return payload;
    }

    private static string CreateTextBody(SupportRequest supportRequest)
    {
        var body = new StringBuilder();

        body.AppendLine("MealList support request");
        body.AppendLine();
        body.AppendLine($"Title: {supportRequest.Title}");
        body.AppendLine($"Description: {supportRequest.Description}");
        body.AppendLine($"Requester type: {(supportRequest.IsAuthenticated ? "Logged-in user" : "Guest")}");
        body.AppendLine($"Name: {supportRequest.Name ?? "Not provided"}");
        body.AppendLine($"Email: {supportRequest.Email ?? "Not provided"}");
        body.AppendLine($"Phone: {supportRequest.Phone ?? "Not provided"}");
        body.AppendLine($"Date/time UTC: {DateTime.UtcNow:O}");

        if (supportRequest.IsAuthenticated)
        {
            body.AppendLine($"Authenticated user ID: {supportRequest.UserId}");
        }

        if (supportRequest.Attachment is not null)
        {
            body.AppendLine($"Image attachment: {supportRequest.Attachment.FileName}");
        }

        return body.ToString();
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.SenderEmail)
            || string.IsNullOrWhiteSpace(_options.SenderName)
            || string.IsNullOrWhiteSpace(_options.ToEmail))
        {
            _logger.LogError("Brevo support email configuration is incomplete.");

            throw new SupportRequestEmailException(
                "Brevo support email configuration is incomplete.");
        }
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

    private class BrevoEmailPayload
    {
        [JsonPropertyName("sender")]
        public BrevoEmailContact Sender { get; set; } = new(string.Empty);

        [JsonPropertyName("to")]
        public List<BrevoEmailContact> To { get; set; } = [];

        [JsonPropertyName("replyTo")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BrevoEmailContact? ReplyTo { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("textContent")]
        public string TextContent { get; set; } = string.Empty;

        [JsonPropertyName("attachment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<BrevoEmailAttachment>? Attachment { get; set; }
    }

    private record BrevoEmailContact(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Name = null);

    private record BrevoEmailAttachment(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("content")] string Content);
}
