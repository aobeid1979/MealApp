namespace MealApp.Services.Email;

/// <summary>
/// Development-only sender: writes the verification code to the backend console
/// instead of emailing it. No email service or credentials are needed.
/// </summary>
public class ConsoleVerificationCodeSender(
    ILogger<ConsoleVerificationCodeSender> logger,
    IWebHostEnvironment environment) : IVerificationCodeSender
{
    public Task SendAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "DEVELOPMENT ONLY - email verification code for {Email}: {Code}",
                email,
                code);
        }
        else
        {
            logger.LogError(
                "No email service is configured, so the verification code for {Email} was not sent. " +
                "Register a real IVerificationCodeSender before enabling EmailVerification outside Development.",
                email);
        }

        return Task.CompletedTask;
    }
}
