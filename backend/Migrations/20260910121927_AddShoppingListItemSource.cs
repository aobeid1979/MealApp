using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealApp.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListItemSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGenerated",
                table: "ShoppingListItems",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGenerated",
                table: "ShoppingListItems");
        }
    }
}
