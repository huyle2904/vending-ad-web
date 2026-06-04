using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VendingAdSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Admins"" (""Email"", ""PasswordHash"", ""FullName"", ""Role"", ""CreatedAt"", ""IsActive"")
                SELECT 'admin', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', 'Administrator', 'Admin', NOW() AT TIME ZONE 'UTC', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Admins"" WHERE ""Email"" = 'admin');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""Admins"" WHERE ""Email"" = 'admin';");
        }
    }
}
