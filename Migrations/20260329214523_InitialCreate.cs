using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookmakers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_bookmaker_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookmakers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    flag_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supported_leagues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    league_api_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supported_leagues", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_states",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    league_api_id = table.Column<long>(type: "bigint", nullable: true),
                    season = table.Column<int>(type: "integer", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leagues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_league_id = table.Column<long>(type: "bigint", nullable: false),
                    country_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leagues", x => x.id);
                    table.ForeignKey(
                        name: "FK_leagues_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_team_id = table.Column<long>(type: "bigint", nullable: false),
                    country_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_teams_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fixtures",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    league_id = table.Column<long>(type: "bigint", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    kickoff_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    home_team_id = table.Column<long>(type: "bigint", nullable: false),
                    away_team_id = table.Column<long>(type: "bigint", nullable: false),
                    home_goals = table.Column<int>(type: "integer", nullable: true),
                    away_goals = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixtures", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixtures_leagues_league_id",
                        column: x => x.league_id,
                        principalTable: "leagues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fixtures_teams_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fixtures_teams_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pre_match_odds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    bookmaker_id = table.Column<long>(type: "bigint", nullable: false),
                    market_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    home_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    draw_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    away_odd = table.Column<decimal>(type: "numeric", nullable: true),
                    collected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pre_match_odds", x => x.id);
                    table.ForeignKey(
                        name: "FK_pre_match_odds_bookmakers_bookmaker_id",
                        column: x => x.bookmaker_id,
                        principalTable: "bookmakers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pre_match_odds_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookmakers_api_bookmaker_id",
                table: "bookmakers",
                column: "api_bookmaker_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_countries_code",
                table: "countries",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "IX_countries_name",
                table: "countries",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_api_fixture_id",
                table: "fixtures",
                column: "api_fixture_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_away_team_id",
                table: "fixtures",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_home_team_id",
                table: "fixtures",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_kickoff_at",
                table: "fixtures",
                column: "kickoff_at");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_league_id",
                table: "fixtures",
                column: "league_id");

            migrationBuilder.CreateIndex(
                name: "IX_leagues_api_league_id_season",
                table: "leagues",
                columns: new[] { "api_league_id", "season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leagues_country_id",
                table: "leagues",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_pre_match_odds_bookmaker_id",
                table: "pre_match_odds",
                column: "bookmaker_id");

            migrationBuilder.CreateIndex(
                name: "IX_pre_match_odds_fixture_id",
                table: "pre_match_odds",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "IX_pre_match_odds_fixture_id_bookmaker_id_market_name_collecte~",
                table: "pre_match_odds",
                columns: new[] { "fixture_id", "bookmaker_id", "market_name", "collected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supported_leagues_league_api_id_season",
                table: "supported_leagues",
                columns: new[] { "league_api_id", "season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_states_entity_type_league_api_id_season",
                table: "sync_states",
                columns: new[] { "entity_type", "league_api_id", "season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_api_team_id",
                table: "teams",
                column: "api_team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_country_id",
                table: "teams",
                column: "country_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pre_match_odds");

            migrationBuilder.DropTable(
                name: "supported_leagues");

            migrationBuilder.DropTable(
                name: "sync_states");

            migrationBuilder.DropTable(
                name: "bookmakers");

            migrationBuilder.DropTable(
                name: "fixtures");

            migrationBuilder.DropTable(
                name: "leagues");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "countries");
        }
    }
}
