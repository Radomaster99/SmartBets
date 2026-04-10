using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage11TheOddsApiLiveOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "the_odds_live_odds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sport_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bookmaker_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bookmaker_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    market_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    market_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    outcome_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    point = table.Column<decimal>(type: "numeric", nullable: true),
                    price = table.Column<decimal>(type: "numeric", nullable: true),
                    last_update_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_the_odds_live_odds", x => x.id);
                    table.ForeignKey(
                        name: "FK_the_odds_live_odds_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_live_odds_collected_at_utc",
                table: "the_odds_live_odds",
                column: "collected_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_live_odds_fixture_id",
                table: "the_odds_live_odds",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_live_odds_fixture_id_bookmaker_key_market_key_coll~",
                table: "the_odds_live_odds",
                columns: new[] { "fixture_id", "bookmaker_key", "market_key", "collected_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_live_odds_fixture_id_bookmaker_key_market_key_outc~",
                table: "the_odds_live_odds",
                columns: new[] { "fixture_id", "bookmaker_key", "market_key", "outcome_name", "point", "collected_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "the_odds_live_odds");
        }
    }
}
