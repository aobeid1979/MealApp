namespace MealApp.DTOs;

public class ShoppingListResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<ShoppingListItemResponseDto> Items { get; set; } = [];
}
