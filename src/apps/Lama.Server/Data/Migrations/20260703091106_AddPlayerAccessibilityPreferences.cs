using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lama.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAccessibilityPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accessibility_preferences_json",
                schema: "rating",
                table: "players",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accessibility_preferences_json",
                schema: "rating",
                table: "players");
        }
    }
}
