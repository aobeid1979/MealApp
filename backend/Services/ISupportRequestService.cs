using MealApp.Services.Models;

namespace MealApp.Services;

public interface ISupportRequestService
{
    Task ReceiveAsync(SupportRequest supportRequest);
}
