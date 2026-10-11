namespace MealApp.Services;

public sealed class SupportEmailUnavailableException : Exception
{
    public SupportEmailUnavailableException()
        : base("Support email delivery is temporarily unavailable. Please try again later.")
    {
    }
}
