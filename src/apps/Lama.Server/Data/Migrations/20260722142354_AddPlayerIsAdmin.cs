using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lama.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerIsAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                schema: "rating",
                table: "players",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                schema: "rating",
                table: "players");
        }
    }
}
