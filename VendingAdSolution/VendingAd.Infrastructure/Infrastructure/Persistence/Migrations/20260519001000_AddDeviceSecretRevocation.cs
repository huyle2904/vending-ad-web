using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendingAdSystem.Infrastructure.Persistence.Migrations
{
    [Migration("20260519001000_AddDeviceSecretRevocation")]
    public partial class AddDeviceSecretRevocation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceSecretRevokedAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceSecretRevokedAt",
                table: "Devices");
        }
    }
}
