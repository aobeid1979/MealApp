namespace MealApp.Models;

public class ApiResponse
{
    public bool IsSuccess { get; set; }

    public object? Result { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public int StatusCode { get; set; }
}
