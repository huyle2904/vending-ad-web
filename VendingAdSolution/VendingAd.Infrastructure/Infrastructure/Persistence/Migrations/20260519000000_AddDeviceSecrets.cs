using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendingAdSystem.Infrastructure.Persistence.Migrations
{
    [Migration("20260519000000_AddDeviceSecrets")]
    public partial class AddDeviceSecrets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceSecretHash",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceSecretCreatedAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceSecretHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceSecretCreatedAt",
                table: "Devices");
        }
    }
}
