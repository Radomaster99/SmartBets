using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage4Analytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "league_rounds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    round_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_rounds", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_rounds_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "league_top_assists",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    api_player_id = table.Column<long>(type: "bigint", nullable: false),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    player_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    team_api_id = table.Column<long>(type: "bigint", nullable: true),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    appearances = table.Column<int>(type: "integer", nullable: true),
                    minutes = table.Column<int>(type: "integer", nullable: true),
                    position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    goals = table.Column<int>(type: "integer", nullable: true),
                    assists = table.Column<int>(type: "integer", nullable: true),
                    passes_key = table.Column<int>(type: "integer", nullable: true),
                    chances_created = table.Column<int>(type: "integer", nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_top_assists", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_top_assists_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "league_top_cards",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    combined_rank = table.Column<int>(type: "integer", nullable: false),
                    yellow_rank = table.Column<int>(type: "integer", nullable: true),
                    red_rank = table.Column<int>(type: "integer", nullable: true),
                    api_player_id = table.Column<long>(type: "bigint", nullable: false),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    player_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    team_api_id = table.Column<long>(type: "bigint", nullable: true),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    appearances = table.Column<int>(type: "integer", nullable: true),
                    minutes = table.Column<int>(type: "integer", nullable: true),
                    position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    yellow_cards = table.Column<int>(type: "integer", nullable: false),
                    red_cards = table.Column<int>(type: "integer", nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_top_cards", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_top_cards_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "league_top_scorers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    api_player_id = table.Column<long>(type: "bigint", nullable: false),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    player_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    team_api_id = table.Column<long>(type: "bigint", nullable: true),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    appearances = table.Column<int>(type: "integer", nullable: true),
                    minutes = table.Column<int>(type: "integer", nullable: true),
                    position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    goals = table.Column<int>(type: "integer", nullable: true),
                    assists = table.Column<int>(type: "integer", nullable: true),
                    shots_total = table.Column<int>(type: "integer", nullable: true),
                    shots_on = table.Column<int>(type: "integer", nullable: true),
                    penalties_scored = table.Column<int>(type: "integer", nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_top_scorers", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_top_scorers_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_statistics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    team_id = table.Column<long>(type: "bigint", nullable: false),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    form = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fixtures_played_total = table.Column<int>(type: "integer", nullable: false),
                    fixtures_played_home = table.Column<int>(type: "integer", nullable: false),
                    fixtures_played_away = table.Column<int>(type: "integer", nullable: false),
                    wins_total = table.Column<int>(type: "integer", nullable: false),
                    wins_home = table.Column<int>(type: "integer", nullable: false),
                    wins_away = table.Column<int>(type: "integer", nullable: false),
                    draws_total = table.Column<int>(type: "integer", nullable: false),
                    draws_home = table.Column<int>(type: "integer", nullable: false),
                    draws_away = table.Column<int>(type: "integer", nullable: false),
                    losses_total = table.Column<int>(type: "integer", nullable: false),
                    losses_home = table.Column<int>(type: "integer", nullable: false),
                    losses_away = table.Column<int>(type: "integer", nullable: false),
                    goals_for_total = table.Column<int>(type: "integer", nullable: false),
                    goals_for_home = table.Column<int>(type: "integer", nullable: false),
                    goals_for_away = table.Column<int>(type: "integer", nullable: false),
                    goals_for_average_total = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    goals_for_average_home = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    goals_for_average_away = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    goals_against_total = table.Column<int>(type: "integer", nullable: false),
                    goals_against_home = table.Column<int>(type: "integer", nullable: false),
                    goals_against_away = table.Column<int>(type: "integer", nullable: false),
                    goals_against_average_total = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    goals_against_average_home = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    goals_against_average_away = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    clean_sheets_total = table.Column<int>(type: "integer", nullable: false),
                    clean_sheets_home = table.Column<int>(type: "integer", nullable: false),
                    clean_sheets_away = table.Column<int>(type: "integer", nullable: false),
                    failed_to_score_total = table.Column<int>(type: "integer", nullable: false),
                    failed_to_score_home = table.Column<int>(type: "integer", nullable: false),
                    failed_to_score_away = table.Column<int>(type: "integer", nullable: false),
                    biggest_streak_wins = table.Column<int>(type: "integer", nullable: false),
                    biggest_streak_draws = table.Column<int>(type: "integer", nullable: false),
                    biggest_streak_losses = table.Column<int>(type: "integer", nullable: false),
                    biggest_win_home = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    biggest_win_away = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    biggest_loss_home = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    biggest_loss_away = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    biggest_goals_for_home = table.Column<int>(type: "integer", nullable: true),
                    biggest_goals_for_away = table.Column<int>(type: "integer", nullable: true),
                    biggest_goals_against_home = table.Column<int>(type: "integer", nullable: true),
                    biggest_goals_against_away = table.Column<int>(type: "integer", nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_statistics", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_statistics_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_statistics_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_league_rounds_league_id_season_round_name",
                table: "league_rounds",
                columns: new[] { "league_id", "season", "round_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_rounds_league_id_season_sort_order",
                table: "league_rounds",
                columns: new[] { "league_id", "season", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_league_top_assists_league_id_season_api_player_id",
                table: "league_top_assists",
                columns: new[] { "league_id", "season", "api_player_id" });

            migrationBuilder.CreateIndex(
                name: "IX_league_top_assists_league_id_season_rank",
                table: "league_top_assists",
                columns: new[] { "league_id", "season", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_top_cards_league_id_season_api_player_id",
                table: "league_top_cards",
                columns: new[] { "league_id", "season", "api_player_id" });

            migrationBuilder.CreateIndex(
                name: "IX_league_top_cards_league_id_season_combined_rank",
                table: "league_top_cards",
                columns: new[] { "league_id", "season", "combined_rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_top_scorers_league_id_season_api_player_id",
                table: "league_top_scorers",
                columns: new[] { "league_id", "season", "api_player_id" });

            migrationBuilder.CreateIndex(
                name: "IX_league_top_scorers_league_id_season_rank",
                table: "league_top_scorers",
                columns: new[] { "league_id", "season", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_statistics_league_id",
                table: "team_statistics",
                column: "league_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_statistics_team_id",
                table: "team_statistics",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_statistics_team_id_league_id_season",
                table: "team_statistics",
                columns: new[] { "team_id", "league_id", "season" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "league_rounds");

            migrationBuilder.DropTable(
                name: "league_top_assists");

            migrationBuilder.DropTable(
                name: "league_top_cards");

            migrationBuilder.DropTable(
                name: "league_top_scorers");

            migrationBuilder.DropTable(
                name: "team_statistics");
        }
    }
}
