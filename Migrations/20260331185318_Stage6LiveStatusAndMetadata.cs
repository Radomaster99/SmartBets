using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartBets.Migrations
{
    /// <inheritdoc />
    public partial class Stage6LiveStatusAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "founded",
                table: "teams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_national",
                table: "teams",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_address",
                table: "teams",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "venue_capacity",
                table: "teams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_city",
                table: "teams",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_image_url",
                table: "teams",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_name",
                table: "teams",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_surface",
                table: "teams",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "elapsed",
                table: "fixtures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_live_status_synced_at_utc",
                table: "fixtures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referee",
                table: "fixtures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "round",
                table: "fixtures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status_extra",
                table: "fixtures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_long",
                table: "fixtures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "timezone",
                table: "fixtures",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_city",
                table: "fixtures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "venue_name",
                table: "fixtures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "founded",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "is_national",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "venue_address",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "venue_capacity",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "venue_city",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "venue_image_url",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "venue_name",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "venue_surface",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "elapsed",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "last_live_status_synced_at_utc",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "referee",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "round",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "status_extra",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "status_long",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "timezone",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "venue_city",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "venue_name",
                table: "fixtures");
        }
    }
}
