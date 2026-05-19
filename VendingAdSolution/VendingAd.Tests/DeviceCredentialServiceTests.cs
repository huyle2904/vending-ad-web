using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using Xunit;

namespace VendingAd.Tests;

public class DeviceCredentialServiceTests
{
    [Fact]
    public async Task ValidateSecretAsync_ReturnsTrueOnlyForAssignedDeviceSecret()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new DeviceCredentialService(
            new Repository<Device>(context),
            new PasswordHashingService());

        var secret = service.GenerateSecret();
        var device = new Device
        {
            DeviceCode = "DEV-001",
            IsActive = true
        };
        service.AssignSecret(device, secret, new DateTime(2026, 5, 19, 0, 0, 0, DateTimeKind.Utc));

        context.Devices.Add(device);
        await context.SaveChangesAsync();

        Assert.StartsWith("vad_", secret);
        Assert.True(await service.ValidateSecretAsync("DEV-001", secret));
        Assert.False(await service.ValidateSecretAsync("DEV-001", "wrong-secret"));
        Assert.False(await service.ValidateSecretAsync("UNKNOWN", secret));
    }
}
