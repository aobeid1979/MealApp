using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomShoppingListItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_Ingredients_IngredientId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_Units_UnitId",
                table: "ShoppingListItems");

            migrationBuilder.AlterColumn<int>(
                name: "UnitId",
                table: "ShoppingListItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ShoppingListItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "IngredientId",
                table: "ShoppingListItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CustomName",
                table: "ShoppingListItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_Ingredients_IngredientId",
                table: "ShoppingListItems",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_Units_UnitId",
                table: "ShoppingListItems",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_Ingredients_IngredientId",
                table: "ShoppingListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_Units_UnitId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "CustomName",
                table: "ShoppingListItems");

            migrationBuilder.AlterColumn<int>(
                name: "UnitId",
                table: "ShoppingListItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ShoppingListItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IngredientId",
                table: "ShoppingListItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_Ingredients_IngredientId",
                table: "ShoppingListItems",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_Units_UnitId",
                table: "ShoppingListItems",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
