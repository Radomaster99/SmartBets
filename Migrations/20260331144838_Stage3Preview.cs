using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage3Preview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_injuries_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_prediction_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fixture_injuries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    api_team_id = table.Column<long>(type: "bigint", nullable: true),
                    team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    player_api_id = table.Column<long>(type: "bigint", nullable: true),
                    player_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    player_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_injuries", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixture_injuries_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixture_predictions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    winner_team_api_id = table.Column<long>(type: "bigint", nullable: true),
                    winner_team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    winner_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    win_or_draw = table.Column<bool>(type: "boolean", nullable: true),
                    under_over = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    advice = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    goals_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    goals_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    percent_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    percent_draw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    percent_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_form_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_form_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_attack_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_attack_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_defence_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_defence_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_poisson_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_poisson_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_head_to_head_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_head_to_head_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_goals_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_goals_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_total_home = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comparison_total_away = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_predictions", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixture_predictions_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_injuries_fixture_id",
                table: "fixture_injuries",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixture_injuries_fixture_id_api_team_id",
                table: "fixture_injuries",
                columns: new[] { "fixture_id", "api_team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_predictions_fixture_id",
                table: "fixture_predictions",
                column: "fixture_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixture_injuries");

            migrationBuilder.DropTable(
                name: "fixture_predictions");

            migrationBuilder.DropColumn(
                name: "last_injuries_synced_at_utc",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "last_prediction_synced_at_utc",
                table: "fixtures");
        }
    }
}
