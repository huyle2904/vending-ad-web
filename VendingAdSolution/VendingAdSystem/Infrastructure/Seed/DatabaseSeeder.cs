using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();

        try
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Username TEXT NOT NULL DEFAULT ''");
        }
        catch { }

        db.Database.ExecuteSqlRaw("UPDATE Users SET Username = Email WHERE Username = ''");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users (Username)");

        try { db.Database.ExecuteSqlRaw("ALTER TABLE Devices ADD COLUMN UserId INTEGER NULL"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Medias ADD COLUMN UserId INTEGER NULL"); } catch { }

        if (!db.Admins.Any())
        {
            db.Admins.Add(new Admin
            {
                Email = "admin@admin",
                PasswordHash = HashPassword("admin@admin"),
                FullName = "System Administrator",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            db.SaveChanges();
        }

        if (!db.Users.Any())
        {
            db.Users.AddRange(new User { Username = "test", Email = "test@test", PasswordHash = HashPassword("test@test"), FullName = "Demo User", IsActive = true });
            db.SaveChanges();
        }

        var users = db.Users.OrderBy(u => u.Id).ToList();
        if (users.Any())
        {
            db.Database.ExecuteSqlRaw("UPDATE Devices SET UserId = {0} WHERE UserId IS NULL", users[0].Id);
            db.Database.ExecuteSqlRaw("UPDATE Medias SET UserId = {0} WHERE UserId IS NULL", users[0].Id);
        }

        if (users.Any() && !db.Devices.Any())
        {
            db.Devices.AddRange(
                new Device { DeviceCode = "TAB-01", UserId = users[0].Id, Location = "Vincom Center", LastSeen = DateTime.UtcNow.AddMinutes(-2), IsActive = true },
                new Device { DeviceCode = "TAB-02", UserId = users[0].Id, Location = "Ben Thanh Market", LastSeen = DateTime.UtcNow.AddMinutes(-5), IsActive = true }
            );
            db.SaveChanges();
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
