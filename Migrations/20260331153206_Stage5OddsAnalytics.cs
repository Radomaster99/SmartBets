using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage5OddsAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "market_consensus",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    market_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sample_size = table.Column<int>(type: "integer", nullable: false),
                    opening_home_consensus_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    opening_draw_consensus_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    opening_away_consensus_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    latest_home_consensus_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    latest_draw_consensus_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    latest_away_consensus_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    best_home_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    best_draw_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    best_away_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    best_home_bookmaker_id = table.Column<long>(type: "bigint", nullable: true),
                    best_draw_bookmaker_id = table.Column<long>(type: "bigint", nullable: true),
                    best_away_bookmaker_id = table.Column<long>(type: "bigint", nullable: true),
                    max_home_spread = table.Column<decimal>(type: "numeric", nullable: true),
                    max_draw_spread = table.Column<decimal>(type: "numeric", nullable: true),
                    max_away_spread = table.Column<decimal>(type: "numeric", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_consensus", x => x.id);
                    table.ForeignKey(
                        name: "FK_market_consensus_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "odds_movements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    bookmaker_id = table.Column<long>(type: "bigint", nullable: false),
                    market_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    snapshot_count = table.Column<int>(type: "integer", nullable: false),
                    first_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    home_delta = table.Column<decimal>(type: "numeric", nullable: true),
                    draw_delta = table.Column<decimal>(type: "numeric", nullable: true),
                    away_delta = table.Column<decimal>(type: "numeric", nullable: true),
                    home_change_percent = table.Column<decimal>(type: "numeric", nullable: true),
                    draw_change_percent = table.Column<decimal>(type: "numeric", nullable: true),
                    away_change_percent = table.Column<decimal>(type: "numeric", nullable: true),
                    home_swing = table.Column<decimal>(type: "numeric", nullable: true),
                    draw_swing = table.Column<decimal>(type: "numeric", nullable: true),
                    away_swing = table.Column<decimal>(type: "numeric", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_odds_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_odds_movements_bookmakers_bookmaker_id",
                        column: x => x.bookmaker_id,
                        principalTable: "bookmakers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_odds_movements_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "odds_open_close",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    bookmaker_id = table.Column<long>(type: "bigint", nullable: false),
                    market_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    snapshot_count = table.Column<int>(type: "integer", nullable: false),
                    opening_home_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    opening_draw_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    opening_away_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    opening_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    latest_home_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    latest_draw_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    latest_away_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    latest_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    peak_home_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    peak_draw_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    peak_away_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    peak_home_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    peak_draw_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    peak_away_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closing_home_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    closing_draw_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    closing_away_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    closing_collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_odds_open_close", x => x.id);
                    table.ForeignKey(
                        name: "FK_odds_open_close_bookmakers_bookmaker_id",
                        column: x => x.bookmaker_id,
                        principalTable: "bookmakers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_odds_open_close_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_consensus_fixture_id_market_name",
                table: "market_consensus",
                columns: new[] { "fixture_id", "market_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_odds_movements_bookmaker_id",
                table: "odds_movements",
                column: "bookmaker_id");

            migrationBuilder.CreateIndex(
                name: "IX_odds_movements_fixture_id_bookmaker_id_market_name",
                table: "odds_movements",
                columns: new[] { "fixture_id", "bookmaker_id", "market_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_odds_movements_fixture_id_market_name",
                table: "odds_movements",
                columns: new[] { "fixture_id", "market_name" });

            migrationBuilder.CreateIndex(
                name: "IX_odds_open_close_bookmaker_id",
                table: "odds_open_close",
                column: "bookmaker_id");

            migrationBuilder.CreateIndex(
                name: "IX_odds_open_close_fixture_id_bookmaker_id_market_name",
                table: "odds_open_close",
                columns: new[] { "fixture_id", "bookmaker_id", "market_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_odds_open_close_fixture_id_market_name",
                table: "odds_open_close",
                columns: new[] { "fixture_id", "market_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_consensus");

            migrationBuilder.DropTable(
                name: "odds_movements");

            migrationBuilder.DropTable(
                name: "odds_open_close");
        }
    }
}
