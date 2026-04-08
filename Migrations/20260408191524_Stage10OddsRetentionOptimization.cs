using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage10OddsRetentionOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_live_odds_collected_at_utc",
                table: "live_odds",
                column: "collected_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_live_odds_collected_at_utc",
                table: "live_odds");
        }
    }
}
