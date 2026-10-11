using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MealApp.Services.Models;

public sealed class BrevoEmailOptionsValidator : IValidateOptions<BrevoEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, BrevoEmailOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            errors.Add("BrevoEmail__ApiKey is required when BrevoEmail__Enabled=true.");
        if (string.IsNullOrWhiteSpace(options.SenderName))
            errors.Add("BrevoEmail__SenderName is required when BrevoEmail__Enabled=true.");

        ValidateEmail(options.SenderEmail, "BrevoEmail__SenderEmail", errors);
        ValidateEmail(options.ToEmail, "BrevoEmail__ToEmail", errors);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateEmail(string email, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(email)
            || !MailAddress.TryCreate(email, out var address)
            || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{key} must be a valid email address when BrevoEmail__Enabled=true.");
        }
    }
}
