using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendingAdSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaTypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "Medias",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Medias");
        }
    }
}
