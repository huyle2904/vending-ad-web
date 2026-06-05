using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendingAdSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePlaybackModeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalFileName",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocalFileStartedUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaybackMode",
                table: "Devices",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalFileName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LocalFileStartedUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PlaybackMode",
                table: "Devices");
        }
    }
}
