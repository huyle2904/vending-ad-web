using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IDeviceCredentialService
{
    string GenerateSecret();
    void AssignSecret(Device device, string secret, DateTime utcNow);
    Task<bool> ValidateSecretAsync(string deviceCode, string? providedSecret);
}

public sealed class DeviceCredentialService : IDeviceCredentialService
{
    private const string SecretPrefix = "vad_";
    private readonly IRepository<Device> _devices;
    private readonly IPasswordHashingService _passwordHashingService;

    public DeviceCredentialService(IRepository<Device> devices, IPasswordHashingService passwordHashingService)
    {
        _devices = devices;
        _passwordHashingService = passwordHashingService;
    }

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return SecretPrefix + encoded;
    }

    public void AssignSecret(Device device, string secret, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Device secret is required.", nameof(secret));

        device.DeviceSecretHash = _passwordHashingService.HashPassword(secret);
        device.DeviceSecretCreatedAt = utcNow;
    }

    public async Task<bool> ValidateSecretAsync(string deviceCode, string? providedSecret)
    {
        if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(providedSecret))
            return false;

        var normalizedCode = deviceCode.Trim();
        var device = await _devices.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode && d.IsActive);

        if (device == null || string.IsNullOrWhiteSpace(device.DeviceSecretHash))
            return false;

        var result = _passwordHashingService.VerifyPassword(device.DeviceSecretHash, providedSecret.Trim());
        return result != PasswordVerificationResult.Failed;
    }
}
