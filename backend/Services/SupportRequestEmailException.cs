namespace MealApp.Services;

public class SupportRequestEmailException : Exception
{
    public SupportRequestEmailException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
