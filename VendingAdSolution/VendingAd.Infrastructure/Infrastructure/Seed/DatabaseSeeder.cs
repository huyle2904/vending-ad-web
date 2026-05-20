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
            var devices = new List<Device>();
            var locations = new[]
            {
                "Vincom Center", "Ben Thanh Market", "Bitexco Tower", "Landmark 81",
                "Saigon Centre", "Diamond Plaza", "Crescent Mall", "SC VivoCity",
                "AEON Mall Binh Tan", "AEON Mall Tan Phu Celadon", "Emart Go Vap",
                "Lotte Mart Thu Duc", "Co.op Mart Nguyen Dinh Chieu", "Big C An Suong",
                "Vincom Mega Mall Thao Dien", "Parkson Hung Vuong", "Nowzone Fashion Mall",
                "Takashi Shopping Mall", "Sense City", "Indochina Plaza"
            };

            for (int i = 1; i <= 20; i++)
            {
                var deviceCode = $"TAB-{i:D2}";
                devices.Add(new Device
                {
                    DeviceCode = deviceCode,
                    UserId = users[0].Id,
                    Location = locations[i - 1],
                    LastSeen = utcNow.AddMinutes(-i),
                    IsActive = true,
                    DeviceSecretHash = HashPassword(GetDemoDeviceSecret(deviceCode)),
                    DeviceSecretCreatedAt = utcNow
                });
            }

            db.Devices.AddRange(devices);
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
        var demoDeviceCodes = new[] { "CLAIM-TEST-290403", "CLAIM-TEST-210603" };
        var allTabDevices = Enumerable.Range(1, 20).Select(i => $"TAB-{i:D2}").ToArray();
        var deviceCodesToCheck = demoDeviceCodes.Concat(allTabDevices).ToArray();

        var devices = db.Devices
            .Where(d => deviceCodesToCheck.Contains(d.DeviceCode) && string.IsNullOrEmpty(d.DeviceSecretHash))
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
