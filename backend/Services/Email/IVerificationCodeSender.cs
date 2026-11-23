namespace MealApp.Services.Email;

/// <summary>
/// Sends the 6-digit email verification code to a user.
/// Swap the registered implementation in Program.cs to use a real email service later.
/// </summary>
public interface IVerificationCodeSender
{
    Task SendAsync(string email, string code, CancellationToken cancellationToken = default);
}
