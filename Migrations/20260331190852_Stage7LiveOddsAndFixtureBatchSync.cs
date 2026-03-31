using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage7LiveOddsAndFixtureBatchSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_bet_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_bet_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_bet_types", x => x.id);
                    table.UniqueConstraint("AK_live_bet_types_api_bet_id", x => x.api_bet_id);
                });

            migrationBuilder.CreateTable(
                name: "live_odds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    bookmaker_id = table.Column<long>(type: "bigint", nullable: false),
                    api_bet_id = table.Column<long>(type: "bigint", nullable: false),
                    bet_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    outcome_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    line = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    odd = table.Column<decimal>(type: "numeric", nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: true),
                    stopped = table.Column<bool>(type: "boolean", nullable: true),
                    blocked = table.Column<bool>(type: "boolean", nullable: true),
                    finished = table.Column<bool>(type: "boolean", nullable: true),
                    collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_odds", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_odds_bookmakers_bookmaker_id",
                        column: x => x.bookmaker_id,
                        principalTable: "bookmakers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_odds_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_live_odds_live_bet_types_api_bet_id",
                        column: x => x.api_bet_id,
                        principalTable: "live_bet_types",
                        principalColumn: "api_bet_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_bet_types_api_bet_id",
                table: "live_bet_types",
                column: "api_bet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_odds_api_bet_id",
                table: "live_odds",
                column: "api_bet_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_odds_bookmaker_id",
                table: "live_odds",
                column: "bookmaker_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_odds_fixture_id",
                table: "live_odds",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_odds_fixture_id_bookmaker_id_api_bet_id_outcome_label_~",
                table: "live_odds",
                columns: new[] { "fixture_id", "bookmaker_id", "api_bet_id", "outcome_label", "line", "collected_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_odds");

            migrationBuilder.DropTable(
                name: "live_bet_types");
        }
    }
}
