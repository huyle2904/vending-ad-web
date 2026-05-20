using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Infrastructure.Seed;

public static class DatabaseSeeder
{
    private static readonly IPasswordHashingService PasswordHashingService = new PasswordHashingService();

    public static void Seed(AppDbContext db)
    {
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

        if (users.Any() && !db.Devices.Any())
        {
            var utcNow = DateTime.UtcNow;
            db.Devices.AddRange(
                new Device { DeviceCode = "TAB-01", UserId = users[0].Id, Location = "Vincom Center", LastSeen = utcNow.AddMinutes(-2), IsActive = true, DeviceSecretHash = HashPassword(GetDemoDeviceSecret("TAB-01")), DeviceSecretCreatedAt = utcNow },
                new Device { DeviceCode = "TAB-02", UserId = users[0].Id, Location = "Ben Thanh Market", LastSeen = utcNow.AddMinutes(-5), IsActive = true, DeviceSecretHash = HashPassword(GetDemoDeviceSecret("TAB-02")), DeviceSecretCreatedAt = utcNow }
            );
            db.SaveChanges();
        }

        SeedClaimDevice(db, "CLAIM-TEST-290403", "Máy vending test 290403", "290403");
        SeedClaimDevice(db, "CLAIM-TEST-210603", "Máy vending test 210603", "210603");
        EnsureDemoDeviceSecrets(db);
    }

    private static void SeedClaimDevice(AppDbContext db, string deviceCode, string location, string claimCode)
    {
        if (db.Devices.Any(d => d.DeviceCode == deviceCode))
            return;

        db.Devices.Add(new Device
        {
            DeviceCode = deviceCode,
            Location = location,
            ClaimCode = claimCode,
            UserId = null,
            IsActive = true,
            LastSeen = DateTime.UtcNow,
            DeviceSecretHash = HashPassword(GetDemoDeviceSecret(deviceCode)),
            DeviceSecretCreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static void EnsureDemoDeviceSecrets(AppDbContext db)
    {
        var demoDeviceCodes = new[] { "TAB-01", "TAB-02", "CLAIM-TEST-290403", "CLAIM-TEST-210603" };
        var devices = db.Devices
            .Where(d => demoDeviceCodes.Contains(d.DeviceCode) && string.IsNullOrEmpty(d.DeviceSecretHash))
            .ToList();

        if (!devices.Any())
            return;

        var utcNow = DateTime.UtcNow;
        foreach (var device in devices)
        {
            device.DeviceSecretHash = HashPassword(GetDemoDeviceSecret(device.DeviceCode));
            device.DeviceSecretCreatedAt = utcNow;
        }

        db.SaveChanges();
    }

    private static string HashPassword(string password)
    {
        return PasswordHashingService.HashPassword(password);
    }

    private static string GetDemoDeviceSecret(string deviceCode)
    {
        return $"dev-secret-{deviceCode}";
    }
}
