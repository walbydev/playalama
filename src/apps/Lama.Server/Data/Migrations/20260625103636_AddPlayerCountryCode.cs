using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lama.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCountryCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "country_code",
                schema: "rating",
                table: "players",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "country_code",
                schema: "rating",
                table: "players");
        }
    }
}
