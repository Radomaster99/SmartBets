using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage12TheOddsLeagueMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "the_odds_league_mappings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_football_league_id = table.Column<long>(type: "bigint", nullable: false),
                    league_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    the_odds_sport_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resolution_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confidence = table.Column<int>(type: "integer", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_the_odds_league_mappings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_league_mappings_api_football_league_id",
                table: "the_odds_league_mappings",
                column: "api_football_league_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_league_mappings_resolution_source",
                table: "the_odds_league_mappings",
                column: "resolution_source");

            migrationBuilder.CreateIndex(
                name: "IX_the_odds_league_mappings_the_odds_sport_key",
                table: "the_odds_league_mappings",
                column: "the_odds_sport_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "the_odds_league_mappings");
        }
    }
}
