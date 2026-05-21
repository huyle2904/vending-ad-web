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
            new PasswordHashingService(),
            NullAuditService.Instance);

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

    [Fact]
    public async Task RotateAndRevokeSecretAsync_InvalidatesOldSecretAndCanRevokeCurrentSecret()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var auditService = new RecordingAuditService();
        var service = new DeviceCredentialService(
            new Repository<Device>(context),
            new PasswordHashingService(),
            auditService);

        const string oldSecret = "old-secret";
        var device = new Device
        {
            DeviceCode = "DEV-ROTATE",
            IsActive = true
        };
        service.AssignSecret(device, oldSecret, new DateTime(2026, 5, 19, 0, 0, 0, DateTimeKind.Utc));

        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var rotation = await service.RotateSecretAsync(device.Id, new DateTime(2026, 5, 19, 1, 0, 0, DateTimeKind.Utc));

        Assert.True(rotation.Success);
        Assert.NotNull(rotation.DeviceSecret);
        Assert.False(await service.ValidateSecretAsync("DEV-ROTATE", oldSecret));
        Assert.True(await service.ValidateSecretAsync("DEV-ROTATE", rotation.DeviceSecret));

        var revoke = await service.RevokeSecretAsync(device.Id, new DateTime(2026, 5, 19, 2, 0, 0, DateTimeKind.Utc));

        Assert.True(revoke.Success);
        Assert.False(await service.ValidateSecretAsync("DEV-ROTATE", rotation.DeviceSecret));
        var reloaded = await context.Devices.SingleAsync(d => d.DeviceCode == "DEV-ROTATE");
        Assert.NotNull(reloaded.DeviceSecretRevokedAt);
        Assert.Null(reloaded.DeviceSecretHash);
        Assert.Equal(
            new[] { AuditActions.RotateDeviceSecret, AuditActions.RevokeDeviceSecret },
            auditService.Entries.Select(entry => entry.Action).ToArray());
    }
}
