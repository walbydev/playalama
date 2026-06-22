using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lama.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialThreeSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "history");

            migrationBuilder.EnsureSchema(
                name: "sessions");

            migrationBuilder.EnsureSchema(
                name: "rating");

            migrationBuilder.CreateTable(
                name: "completed_games",
                schema: "history",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BoardSize = table.Column<int>(type: "integer", nullable: false),
                    RackSize = table.Column<int>(type: "integer", nullable: false),
                    MinWordLength = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Queue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    WinningPlayerId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_completed_games", x => x.GameId);
                });

            migrationBuilder.CreateTable(
                name: "games",
                schema: "sessions",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GameLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BoardSize = table.Column<int>(type: "integer", nullable: false),
                    RackSize = table.Column<int>(type: "integer", nullable: false),
                    MinWordLength = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Queue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.GameId);
                });

            migrationBuilder.CreateTable(
                name: "players",
                schema: "rating",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.PlayerId);
                });

            migrationBuilder.CreateTable(
                name: "player_ratings",
                schema: "rating",
                columns: table => new
                {
                    RatingRecordId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Queue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EloRating = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: false),
                    GamesWon = table.Column<int>(type: "integer", nullable: false),
                    GamesLost = table.Column<int>(type: "integer", nullable: false),
                    GamesAbandoned = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    AvgScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    LastGameDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_ratings", x => x.RatingRecordId);
                    table.ForeignKey(
                        name: "FK_player_ratings_players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "rating",
                        principalTable: "players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_completed_games_EndedAt",
                schema: "history",
                table: "completed_games",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_completed_games_Queue",
                schema: "history",
                table: "completed_games",
                column: "Queue");

            migrationBuilder.CreateIndex(
                name: "IX_games_Queue",
                schema: "sessions",
                table: "games",
                column: "Queue");

            migrationBuilder.CreateIndex(
                name: "IX_games_Status",
                schema: "sessions",
                table: "games",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_games_UpdatedAt",
                schema: "sessions",
                table: "games",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_player_ratings_PlayerId_Queue",
                schema: "rating",
                table: "player_ratings",
                columns: new[] { "PlayerId", "Queue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_ratings_Queue_EloRating",
                schema: "rating",
                table: "player_ratings",
                columns: new[] { "Queue", "EloRating" });

            migrationBuilder.CreateIndex(
                name: "IX_players_Username",
                schema: "rating",
                table: "players",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completed_games",
                schema: "history");

            migrationBuilder.DropTable(
                name: "games",
                schema: "sessions");

            migrationBuilder.DropTable(
                name: "player_ratings",
                schema: "rating");

            migrationBuilder.DropTable(
                name: "players",
                schema: "rating");
        }
    }
}
