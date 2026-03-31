using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage1CoverageAndSyncErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "league_season_coverages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_api_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    has_fixtures = table.Column<bool>(type: "boolean", nullable: false),
                    has_fixture_events = table.Column<bool>(type: "boolean", nullable: false),
                    has_lineups = table.Column<bool>(type: "boolean", nullable: false),
                    has_fixture_statistics = table.Column<bool>(type: "boolean", nullable: false),
                    has_player_statistics = table.Column<bool>(type: "boolean", nullable: false),
                    has_standings = table.Column<bool>(type: "boolean", nullable: false),
                    has_players = table.Column<bool>(type: "boolean", nullable: false),
                    has_top_scorers = table.Column<bool>(type: "boolean", nullable: false),
                    has_top_assists = table.Column<bool>(type: "boolean", nullable: false),
                    has_top_cards = table.Column<bool>(type: "boolean", nullable: false),
                    has_injuries = table.Column<bool>(type: "boolean", nullable: false),
                    has_predictions = table.Column<bool>(type: "boolean", nullable: false),
                    has_odds = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_season_coverages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "standings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    goals_diff = table.Column<int>(type: "integer", nullable: false),
                    group_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    form = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    played = table.Column<int>(type: "integer", nullable: false),
                    win = table.Column<int>(type: "integer", nullable: false),
                    draw = table.Column<int>(type: "integer", nullable: false),
                    lose = table.Column<int>(type: "integer", nullable: false),
                    goals_for = table.Column<int>(type: "integer", nullable: false),
                    goals_against = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_standings", x => x.id);
                    table.ForeignKey(
                        name: "FK_standings_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_standings_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_errors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    league_api_id = table.Column<long>(type: "bigint", nullable: true),
                    season = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_errors", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_league_season_coverages_league_api_id_season",
                table: "league_season_coverages",
                columns: new[] { "league_api_id", "season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_season_coverages_season",
                table: "league_season_coverages",
                column: "season");

            migrationBuilder.CreateIndex(
                name: "IX_standings_league_id",
                table: "standings",
                column: "league_id");

            migrationBuilder.CreateIndex(
                name: "IX_standings_league_id_season_team_id",
                table: "standings",
                columns: new[] { "league_id", "season", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_standings_rank",
                table: "standings",
                column: "rank");

            migrationBuilder.CreateIndex(
                name: "IX_standings_team_id",
                table: "standings",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_errors_entity_type",
                table: "sync_errors",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "IX_sync_errors_entity_type_league_api_id_season",
                table: "sync_errors",
                columns: new[] { "entity_type", "league_api_id", "season" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_errors_occurred_at",
                table: "sync_errors",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "league_season_coverages");

            migrationBuilder.DropTable(
                name: "standings");

            migrationBuilder.DropTable(
                name: "sync_errors");
        }
    }
}
