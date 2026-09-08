using MealApp.Services.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net.Mail;
using System.Text;

namespace MealApp.Services;

public class SupportRequestService : ISupportRequestService
{
    private readonly SupportEmailOptions _options;
    private readonly ILogger<SupportRequestService> _logger;

    public SupportRequestService(
        IOptions<SupportEmailOptions> options,
        ILogger<SupportRequestService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ReceiveAsync(SupportRequest supportRequest)
    {
        ValidateOptions();

        var message = CreateMessage(supportRequest);

        try
        {
            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();

            await smtpClient.ConnectAsync(
                _options.Host,
                _options.Port,
                SecureSocketOptions.StartTls);
            await smtpClient.AuthenticateAsync(_options.Username, _options.Password);
            await smtpClient.SendAsync(message);
            await smtpClient.DisconnectAsync(true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send support request email.");

            throw new SupportRequestEmailException(
                "Support request could not be sent.",
                exception);
        }
    }

    private MimeMessage CreateMessage(SupportRequest supportRequest)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.FromEmail));
        message.To.Add(MailboxAddress.Parse(_options.ToEmail));
        message.Subject = $"[MealList Support] {supportRequest.Title}";

        if (!string.IsNullOrWhiteSpace(supportRequest.Email) && IsValidEmail(supportRequest.Email))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(supportRequest.Email));
        }

        var bodyBuilder = new BodyBuilder
        {
            TextBody = CreateTextBody(supportRequest)
        };

        if (supportRequest.Attachment is not null)
        {
            bodyBuilder.Attachments.Add(
                supportRequest.Attachment.FileName,
                supportRequest.Attachment.Content,
                ContentType.Parse(supportRequest.Attachment.ContentType));
        }

        message.Body = bodyBuilder.ToMessageBody();

        return message;
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
        if (string.IsNullOrWhiteSpace(_options.Host)
            || _options.Port <= 0
            || string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.Password)
            || string.IsNullOrWhiteSpace(_options.FromEmail)
            || string.IsNullOrWhiteSpace(_options.ToEmail))
        {
            _logger.LogError("Support email configuration is incomplete.");

            throw new SupportRequestEmailException(
                "Support email configuration is incomplete.");
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
}
