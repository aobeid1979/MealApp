using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Categories]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Categories] (
                        [Id] int NOT NULL IDENTITY,
                        [Name] nvarchar(max) NOT NULL,
                        [Description] nvarchar(max) NULL,
                        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
                    );
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Categories]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[Categories];
                END
                """);
        }
    }
}
