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
    Task<DeviceSecretRotationResult> RotateSecretAsync(int deviceId, DateTime utcNow);
    Task<DeviceSecretRevokeResult> RevokeSecretAsync(int deviceId, DateTime utcNow);
    Task<bool> ValidateSecretAsync(string deviceCode, string? providedSecret);
}

public sealed record DeviceSecretRotationResult(bool Success, string Message, int? DeviceId = null, string? DeviceCode = null, string? DeviceSecret = null);

public sealed record DeviceSecretRevokeResult(bool Success, string Message, int? DeviceId = null, string? DeviceCode = null);

public sealed class DeviceCredentialService : IDeviceCredentialService
{
    private const string SecretPrefix = "vad_";
    private readonly IRepository<Device> _devices;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IAuditService _auditService;

    public DeviceCredentialService(IRepository<Device> devices, IPasswordHashingService passwordHashingService, IAuditService auditService)
    {
        _devices = devices;
        _passwordHashingService = passwordHashingService;
        _auditService = auditService;
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
        device.DeviceSecretRevokedAt = null;
    }

    public async Task<DeviceSecretRotationResult> RotateSecretAsync(int deviceId, DateTime utcNow)
    {
        var device = await _devices.GetByIdAsync(deviceId);
        if (device == null)
            return new DeviceSecretRotationResult(false, "Không tìm thấy thiết bị.");

        var secret = GenerateSecret();
        AssignSecret(device, secret, utcNow);
        await _devices.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.RotateDeviceSecret,
            TargetType = AuditTargets.Device,
            TargetId = device.Id,
            Details = new
            {
                device.DeviceCode
            }
        });

        return new DeviceSecretRotationResult(
            true,
            "Đã cấp lại secret cho thiết bị.",
            device.Id,
            device.DeviceCode,
            secret);
    }

    public async Task<DeviceSecretRevokeResult> RevokeSecretAsync(int deviceId, DateTime utcNow)
    {
        var device = await _devices.GetByIdAsync(deviceId);
        if (device == null)
            return new DeviceSecretRevokeResult(false, "Không tìm thấy thiết bị.");

        device.DeviceSecretHash = null;
        device.DeviceSecretRevokedAt = utcNow;
        await _devices.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.RevokeDeviceSecret,
            TargetType = AuditTargets.Device,
            TargetId = device.Id,
            Details = new
            {
                device.DeviceCode
            }
        });

        return new DeviceSecretRevokeResult(
            true,
            "Đã thu hồi secret của thiết bị.",
            device.Id,
            device.DeviceCode);
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
