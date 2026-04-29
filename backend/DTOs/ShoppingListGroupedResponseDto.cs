namespace MealApp.DTOs;

public class ShoppingListGroupedResponseDto
{
    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public List<ShoppingListItemResponseDto> Items { get; set; } = [];
}
