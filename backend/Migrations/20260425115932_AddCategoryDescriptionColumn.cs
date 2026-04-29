using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryDescriptionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[dbo].[Categories]', N'Description') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Categories]
                    ADD [Description] nvarchar(max) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[dbo].[Categories]', N'Description') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Categories]
                    DROP COLUMN [Description];
                END
                """);
        }
    }
}
