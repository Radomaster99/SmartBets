using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage2MatchCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_event_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_lineups_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_player_statistics_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_statistics_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "post_finish_match_center_sync_count",
                table: "fixtures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "fixture_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    elapsed = table.Column<int>(type: "integer", nullable: true),
                    extra = table.Column<int>(type: "integer", nullable: true),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    api_team_id = table.Column<long>(type: "bigint", nullable: true),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    player_api_id = table.Column<long>(type: "bigint", nullable: true),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    assist_api_id = table.Column<long>(type: "bigint", nullable: true),
                    assist_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    comments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixture_events_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixture_lineups",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    api_team_id = table.Column<long>(type: "bigint", nullable: false),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    formation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    coach_api_id = table.Column<long>(type: "bigint", nullable: true),
                    coach_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    coach_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_starting = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    player_api_id = table.Column<long>(type: "bigint", nullable: true),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    player_number = table.Column<int>(type: "integer", nullable: true),
                    player_position = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    player_grid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_lineups", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixture_lineups_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixture_player_statistics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    api_team_id = table.Column<long>(type: "bigint", nullable: false),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    player_api_id = table.Column<long>(type: "bigint", nullable: false),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    player_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    minutes = table.Column<int>(type: "integer", nullable: true),
                    number = table.Column<int>(type: "integer", nullable: true),
                    position = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rating = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    is_captain = table.Column<bool>(type: "boolean", nullable: false),
                    is_substitute = table.Column<bool>(type: "boolean", nullable: false),
                    offsides = table.Column<int>(type: "integer", nullable: true),
                    shots_total = table.Column<int>(type: "integer", nullable: true),
                    shots_on = table.Column<int>(type: "integer", nullable: true),
                    goals_total = table.Column<int>(type: "integer", nullable: true),
                    goals_conceded = table.Column<int>(type: "integer", nullable: true),
                    goals_assists = table.Column<int>(type: "integer", nullable: true),
                    goals_saves = table.Column<int>(type: "integer", nullable: true),
                    passes_total = table.Column<int>(type: "integer", nullable: true),
                    passes_key = table.Column<int>(type: "integer", nullable: true),
                    passes_accuracy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tackles_total = table.Column<int>(type: "integer", nullable: true),
                    tackles_blocks = table.Column<int>(type: "integer", nullable: true),
                    tackles_interceptions = table.Column<int>(type: "integer", nullable: true),
                    duels_total = table.Column<int>(type: "integer", nullable: true),
                    duels_won = table.Column<int>(type: "integer", nullable: true),
                    dribbles_attempts = table.Column<int>(type: "integer", nullable: true),
                    dribbles_success = table.Column<int>(type: "integer", nullable: true),
                    dribbles_past = table.Column<int>(type: "integer", nullable: true),
                    fouls_drawn = table.Column<int>(type: "integer", nullable: true),
                    fouls_committed = table.Column<int>(type: "integer", nullable: true),
                    cards_yellow = table.Column<int>(type: "integer", nullable: true),
                    cards_red = table.Column<int>(type: "integer", nullable: true),
                    penalty_won = table.Column<int>(type: "integer", nullable: true),
                    penalty_committed = table.Column<int>(type: "integer", nullable: true),
                    penalty_scored = table.Column<int>(type: "integer", nullable: true),
                    penalty_missed = table.Column<int>(type: "integer", nullable: true),
                    penalty_saved = table.Column<int>(type: "integer", nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_player_statistics", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixture_player_statistics_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixture_statistics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    api_team_id = table.Column<long>(type: "bigint", nullable: false),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_statistics", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixture_statistics_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_events_fixture_id",
                table: "fixture_events",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixture_events_fixture_id_sort_order",
                table: "fixture_events",
                columns: new[] { "fixture_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_lineups_fixture_id",
                table: "fixture_lineups",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixture_lineups_fixture_id_api_team_id_is_starting_sort_ord~",
                table: "fixture_lineups",
                columns: new[] { "fixture_id", "api_team_id", "is_starting", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_player_statistics_fixture_id",
                table: "fixture_player_statistics",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixture_player_statistics_fixture_id_api_team_id_player_api~",
                table: "fixture_player_statistics",
                columns: new[] { "fixture_id", "api_team_id", "player_api_id" });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_statistics_fixture_id",
                table: "fixture_statistics",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixture_statistics_fixture_id_api_team_id_sort_order",
                table: "fixture_statistics",
                columns: new[] { "fixture_id", "api_team_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixture_events");

            migrationBuilder.DropTable(
                name: "fixture_lineups");

            migrationBuilder.DropTable(
                name: "fixture_player_statistics");

            migrationBuilder.DropTable(
                name: "fixture_statistics");

            migrationBuilder.DropColumn(
                name: "last_event_synced_at_utc",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "last_lineups_synced_at_utc",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "last_player_statistics_synced_at_utc",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "last_statistics_synced_at_utc",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "post_finish_match_center_sync_count",
                table: "fixtures");
        }
    }
}
