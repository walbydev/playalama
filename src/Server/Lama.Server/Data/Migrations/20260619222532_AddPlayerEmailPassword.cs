using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lama.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerEmailPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_player_ratings_players_PlayerId",
                schema: "rating",
                table: "player_ratings");

            migrationBuilder.RenameColumn(
                name: "Username",
                schema: "rating",
                table: "players",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "rating",
                table: "players",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "PlayerId",
                schema: "rating",
                table: "players",
                newName: "player_id");

            migrationBuilder.RenameIndex(
                name: "IX_players_Username",
                schema: "rating",
                table: "players",
                newName: "IX_players_username");

            migrationBuilder.RenameColumn(
                name: "Queue",
                schema: "rating",
                table: "player_ratings",
                newName: "queue");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "rating",
                table: "player_ratings",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TotalPoints",
                schema: "rating",
                table: "player_ratings",
                newName: "total_points");

            migrationBuilder.RenameColumn(
                name: "PlayerId",
                schema: "rating",
                table: "player_ratings",
                newName: "player_id");

            migrationBuilder.RenameColumn(
                name: "LastGameDate",
                schema: "rating",
                table: "player_ratings",
                newName: "last_game_date");

            migrationBuilder.RenameColumn(
                name: "GamesWon",
                schema: "rating",
                table: "player_ratings",
                newName: "games_won");

            migrationBuilder.RenameColumn(
                name: "GamesPlayed",
                schema: "rating",
                table: "player_ratings",
                newName: "games_played");

            migrationBuilder.RenameColumn(
                name: "GamesLost",
                schema: "rating",
                table: "player_ratings",
                newName: "games_lost");

            migrationBuilder.RenameColumn(
                name: "GamesAbandoned",
                schema: "rating",
                table: "player_ratings",
                newName: "games_abandoned");

            migrationBuilder.RenameColumn(
                name: "EloRating",
                schema: "rating",
                table: "player_ratings",
                newName: "elo_rating");

            migrationBuilder.RenameColumn(
                name: "AvgScore",
                schema: "rating",
                table: "player_ratings",
                newName: "avg_score");

            migrationBuilder.RenameColumn(
                name: "RatingRecordId",
                schema: "rating",
                table: "player_ratings",
                newName: "rating_record_id");

            migrationBuilder.RenameIndex(
                name: "IX_player_ratings_Queue_EloRating",
                schema: "rating",
                table: "player_ratings",
                newName: "IX_player_ratings_queue_elo_rating");

            migrationBuilder.RenameIndex(
                name: "IX_player_ratings_PlayerId_Queue",
                schema: "rating",
                table: "player_ratings",
                newName: "IX_player_ratings_player_id_queue");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "sessions",
                table: "games",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Queue",
                schema: "sessions",
                table: "games",
                newName: "queue");

            migrationBuilder.RenameColumn(
                name: "Language",
                schema: "sessions",
                table: "games",
                newName: "language");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "sessions",
                table: "games",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "RackSize",
                schema: "sessions",
                table: "games",
                newName: "rack_size");

            migrationBuilder.RenameColumn(
                name: "MinWordLength",
                schema: "sessions",
                table: "games",
                newName: "min_word_length");

            migrationBuilder.RenameColumn(
                name: "GameLevel",
                schema: "sessions",
                table: "games",
                newName: "game_level");

            migrationBuilder.RenameColumn(
                name: "EndedAt",
                schema: "sessions",
                table: "games",
                newName: "ended_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "sessions",
                table: "games",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "BoardSize",
                schema: "sessions",
                table: "games",
                newName: "board_size");

            migrationBuilder.RenameColumn(
                name: "GameId",
                schema: "sessions",
                table: "games",
                newName: "game_id");

            migrationBuilder.RenameIndex(
                name: "IX_games_Status",
                schema: "sessions",
                table: "games",
                newName: "IX_games_status");

            migrationBuilder.RenameIndex(
                name: "IX_games_Queue",
                schema: "sessions",
                table: "games",
                newName: "IX_games_queue");

            migrationBuilder.RenameIndex(
                name: "IX_games_UpdatedAt",
                schema: "sessions",
                table: "games",
                newName: "IX_games_updated_at");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "history",
                table: "completed_games",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Queue",
                schema: "history",
                table: "completed_games",
                newName: "queue");

            migrationBuilder.RenameColumn(
                name: "Language",
                schema: "history",
                table: "completed_games",
                newName: "language");

            migrationBuilder.RenameColumn(
                name: "WinningPlayerId",
                schema: "history",
                table: "completed_games",
                newName: "winning_player_id");

            migrationBuilder.RenameColumn(
                name: "RackSize",
                schema: "history",
                table: "completed_games",
                newName: "rack_size");

            migrationBuilder.RenameColumn(
                name: "MinWordLength",
                schema: "history",
                table: "completed_games",
                newName: "min_word_length");

            migrationBuilder.RenameColumn(
                name: "GameLevel",
                schema: "history",
                table: "completed_games",
                newName: "game_level");

            migrationBuilder.RenameColumn(
                name: "EndedAt",
                schema: "history",
                table: "completed_games",
                newName: "ended_at");

            migrationBuilder.RenameColumn(
                name: "DurationSeconds",
                schema: "history",
                table: "completed_games",
                newName: "duration_seconds");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "history",
                table: "completed_games",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "BoardSize",
                schema: "history",
                table: "completed_games",
                newName: "board_size");

            migrationBuilder.RenameColumn(
                name: "GameId",
                schema: "history",
                table: "completed_games",
                newName: "game_id");

            migrationBuilder.RenameIndex(
                name: "IX_completed_games_Queue",
                schema: "history",
                table: "completed_games",
                newName: "IX_completed_games_queue");

            migrationBuilder.RenameIndex(
                name: "IX_completed_games_EndedAt",
                schema: "history",
                table: "completed_games",
                newName: "IX_completed_games_ended_at");

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "rating",
                table: "players",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "rating",
                table: "players",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "board_state",
                schema: "sessions",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_board_state", x => x.game_id);
                });

            migrationBuilder.CreateTable(
                name: "players_in_game",
                schema: "sessions",
                columns: table => new
                {
                    player_session_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_host = table.Column<bool>(type: "boolean", nullable: false),
                    player_index = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players_in_game", x => x.player_session_id);
                });

            migrationBuilder.CreateTable(
                name: "turn_log",
                schema: "sessions",
                columns: table => new
                {
                    turn_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action_payload = table.Column<string>(type: "jsonb", nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_turn_log", x => x.turn_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_players_email",
                schema: "rating",
                table: "players",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_board_state_updated_at",
                schema: "sessions",
                table: "board_state",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "IX_players_in_game_game_id",
                schema: "sessions",
                table: "players_in_game",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_players_in_game_game_id_player_id",
                schema: "sessions",
                table: "players_in_game",
                columns: new[] { "game_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_in_game_game_id_player_index",
                schema: "sessions",
                table: "players_in_game",
                columns: new[] { "game_id", "player_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_in_game_is_host",
                schema: "sessions",
                table: "players_in_game",
                column: "is_host");

            migrationBuilder.CreateIndex(
                name: "IX_players_in_game_player_id",
                schema: "sessions",
                table: "players_in_game",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_turn_log_executed_at",
                schema: "sessions",
                table: "turn_log",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "IX_turn_log_game_id",
                schema: "sessions",
                table: "turn_log",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_turn_log_game_id_turn_number",
                schema: "sessions",
                table: "turn_log",
                columns: new[] { "game_id", "turn_number" });

            migrationBuilder.CreateIndex(
                name: "IX_turn_log_player_session_id",
                schema: "sessions",
                table: "turn_log",
                column: "player_session_id");

            migrationBuilder.AddForeignKey(
                name: "FK_player_ratings_players_player_id",
                schema: "rating",
                table: "player_ratings",
                column: "player_id",
                principalSchema: "rating",
                principalTable: "players",
                principalColumn: "player_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_player_ratings_players_player_id",
                schema: "rating",
                table: "player_ratings");

            migrationBuilder.DropTable(
                name: "board_state",
                schema: "sessions");

            migrationBuilder.DropTable(
                name: "players_in_game",
                schema: "sessions");

            migrationBuilder.DropTable(
                name: "turn_log",
                schema: "sessions");

            migrationBuilder.DropIndex(
                name: "IX_players_email",
                schema: "rating",
                table: "players");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "rating",
                table: "players");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "rating",
                table: "players");

            migrationBuilder.RenameColumn(
                name: "username",
                schema: "rating",
                table: "players",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "rating",
                table: "players",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "player_id",
                schema: "rating",
                table: "players",
                newName: "PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_players_username",
                schema: "rating",
                table: "players",
                newName: "IX_players_Username");

            migrationBuilder.RenameColumn(
                name: "queue",
                schema: "rating",
                table: "player_ratings",
                newName: "Queue");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                schema: "rating",
                table: "player_ratings",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "total_points",
                schema: "rating",
                table: "player_ratings",
                newName: "TotalPoints");

            migrationBuilder.RenameColumn(
                name: "player_id",
                schema: "rating",
                table: "player_ratings",
                newName: "PlayerId");

            migrationBuilder.RenameColumn(
                name: "last_game_date",
                schema: "rating",
                table: "player_ratings",
                newName: "LastGameDate");

            migrationBuilder.RenameColumn(
                name: "games_won",
                schema: "rating",
                table: "player_ratings",
                newName: "GamesWon");

            migrationBuilder.RenameColumn(
                name: "games_played",
                schema: "rating",
                table: "player_ratings",
                newName: "GamesPlayed");

            migrationBuilder.RenameColumn(
                name: "games_lost",
                schema: "rating",
                table: "player_ratings",
                newName: "GamesLost");

            migrationBuilder.RenameColumn(
                name: "games_abandoned",
                schema: "rating",
                table: "player_ratings",
                newName: "GamesAbandoned");

            migrationBuilder.RenameColumn(
                name: "elo_rating",
                schema: "rating",
                table: "player_ratings",
                newName: "EloRating");

            migrationBuilder.RenameColumn(
                name: "avg_score",
                schema: "rating",
                table: "player_ratings",
                newName: "AvgScore");

            migrationBuilder.RenameColumn(
                name: "rating_record_id",
                schema: "rating",
                table: "player_ratings",
                newName: "RatingRecordId");

            migrationBuilder.RenameIndex(
                name: "IX_player_ratings_queue_elo_rating",
                schema: "rating",
                table: "player_ratings",
                newName: "IX_player_ratings_Queue_EloRating");

            migrationBuilder.RenameIndex(
                name: "IX_player_ratings_player_id_queue",
                schema: "rating",
                table: "player_ratings",
                newName: "IX_player_ratings_PlayerId_Queue");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "sessions",
                table: "games",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "queue",
                schema: "sessions",
                table: "games",
                newName: "Queue");

            migrationBuilder.RenameColumn(
                name: "language",
                schema: "sessions",
                table: "games",
                newName: "Language");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                schema: "sessions",
                table: "games",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "rack_size",
                schema: "sessions",
                table: "games",
                newName: "RackSize");

            migrationBuilder.RenameColumn(
                name: "min_word_length",
                schema: "sessions",
                table: "games",
                newName: "MinWordLength");

            migrationBuilder.RenameColumn(
                name: "game_level",
                schema: "sessions",
                table: "games",
                newName: "GameLevel");

            migrationBuilder.RenameColumn(
                name: "ended_at",
                schema: "sessions",
                table: "games",
                newName: "EndedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "sessions",
                table: "games",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "board_size",
                schema: "sessions",
                table: "games",
                newName: "BoardSize");

            migrationBuilder.RenameColumn(
                name: "game_id",
                schema: "sessions",
                table: "games",
                newName: "GameId");

            migrationBuilder.RenameIndex(
                name: "IX_games_status",
                schema: "sessions",
                table: "games",
                newName: "IX_games_Status");

            migrationBuilder.RenameIndex(
                name: "IX_games_queue",
                schema: "sessions",
                table: "games",
                newName: "IX_games_Queue");

            migrationBuilder.RenameIndex(
                name: "IX_games_updated_at",
                schema: "sessions",
                table: "games",
                newName: "IX_games_UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "history",
                table: "completed_games",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "queue",
                schema: "history",
                table: "completed_games",
                newName: "Queue");

            migrationBuilder.RenameColumn(
                name: "language",
                schema: "history",
                table: "completed_games",
                newName: "Language");

            migrationBuilder.RenameColumn(
                name: "winning_player_id",
                schema: "history",
                table: "completed_games",
                newName: "WinningPlayerId");

            migrationBuilder.RenameColumn(
                name: "rack_size",
                schema: "history",
                table: "completed_games",
                newName: "RackSize");

            migrationBuilder.RenameColumn(
                name: "min_word_length",
                schema: "history",
                table: "completed_games",
                newName: "MinWordLength");

            migrationBuilder.RenameColumn(
                name: "game_level",
                schema: "history",
                table: "completed_games",
                newName: "GameLevel");

            migrationBuilder.RenameColumn(
                name: "ended_at",
                schema: "history",
                table: "completed_games",
                newName: "EndedAt");

            migrationBuilder.RenameColumn(
                name: "duration_seconds",
                schema: "history",
                table: "completed_games",
                newName: "DurationSeconds");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "history",
                table: "completed_games",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "board_size",
                schema: "history",
                table: "completed_games",
                newName: "BoardSize");

            migrationBuilder.RenameColumn(
                name: "game_id",
                schema: "history",
                table: "completed_games",
                newName: "GameId");

            migrationBuilder.RenameIndex(
                name: "IX_completed_games_queue",
                schema: "history",
                table: "completed_games",
                newName: "IX_completed_games_Queue");

            migrationBuilder.RenameIndex(
                name: "IX_completed_games_ended_at",
                schema: "history",
                table: "completed_games",
                newName: "IX_completed_games_EndedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_player_ratings_players_PlayerId",
                schema: "rating",
                table: "player_ratings",
                column: "PlayerId",
                principalSchema: "rating",
                principalTable: "players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
