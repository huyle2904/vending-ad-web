using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IDeviceService
{
    Task<List<Device>> GetAllDevicesAsync();
    Task<List<Device>> GetAllDevicesWithUsersAsync();
    Task<List<Device>> GetUserDevicesAsync(int userId, bool activeOnly = true);
    Task<Device?> GetDeviceForUserAsync(int deviceId, int userId);
    Task<bool> IsDeviceOwnedByUserAsync(string deviceCode, int userId);
    Task<Device?> GetByIdAsync(int id);
    Task<Device?> GetByCodeAsync(string deviceCode);
    Task<string> GenerateDeviceCodeAsync(string deviceName);
    Task<string> GenerateClaimCodeAsync();
    Task<DeviceClaimResult> ClaimAsync(string claimCode, int userId, DateTime utcNow);
    Task AddAsync(Device device);
    void Remove(Device device);
    Task SaveChangesAsync();
}

public class DeviceService : IDeviceService
{
    private const int MaxDeviceNameLength = 100;
    private readonly IRepository<Device> _devices;

    public DeviceService(IRepository<Device> devices)
    {
        _devices = devices;
    }

    public async Task<List<Device>> GetAllDevicesAsync()
        => await _devices.Query().AsNoTracking().ToListAsync();

    public async Task<List<Device>> GetAllDevicesWithUsersAsync()
        => await _devices.Query().AsNoTracking().Include(d => d.User).OrderBy(d => d.DeviceCode).ToListAsync();

    public async Task<List<Device>> GetUserDevicesAsync(int userId, bool activeOnly = true)
    {
        var query = _devices.Query().AsNoTracking().Where(d => d.UserId == userId);
        if (activeOnly)
            query = query.Where(d => d.IsActive);
        return await query.OrderBy(d => d.DeviceCode).ToListAsync();
    }

    public async Task<Device?> GetDeviceForUserAsync(int deviceId, int userId)
        => await _devices.Query().FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);

    public async Task<bool> IsDeviceOwnedByUserAsync(string deviceCode, int userId)
        => await _devices.Query().AsNoTracking().AnyAsync(d => d.DeviceCode == deviceCode && d.UserId == userId && d.IsActive);

    public Task<Device?> GetByIdAsync(int id) => _devices.GetByIdAsync(id);
    public Task<Device?> GetByCodeAsync(string deviceCode) => _devices.Query().FirstOrDefaultAsync(d => d.DeviceCode == deviceCode);

    public async Task<string> GenerateDeviceCodeAsync(string deviceName)
    {
        var normalizedName = NormalizeDeviceName(deviceName);
        if (normalizedName.Length > MaxDeviceNameLength)
            normalizedName = normalizedName[..MaxDeviceNameLength];

        var slug = BuildDeviceCodeSlug(normalizedName);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "DEVICE";

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var suffix = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var candidate = $"{slug}-{suffix}";
            var exists = await _devices.Query().AnyAsync(d => d.DeviceCode == candidate);
            if (!exists)
                return candidate;
        }

        throw new InvalidOperationException("Không thể tạo mã thiết bị. Vui lòng thử lại.");
    }

    public async Task<string> GenerateClaimCodeAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var exists = await _devices.Query().AnyAsync(d => d.ClaimCode == code);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Không thể tạo mã liên kết thiết bị. Vui lòng thử lại.");
    }

    public async Task<DeviceClaimResult> ClaimAsync(string claimCode, int userId, DateTime utcNow)
    {
        var normalizedCode = claimCode.Trim();
        if (!Regex.IsMatch(normalizedCode, "^\\d{6}$"))
            return new DeviceClaimResult { Success = false, Message = "Mã liên kết phải gồm đúng 6 chữ số." };

        var updated = await _devices.Query()
            .Where(d => d.ClaimCode == normalizedCode && d.UserId == null && d.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.UserId, userId)
                .SetProperty(d => d.ClaimedAt, utcNow)
                .SetProperty(d => d.ClaimCode, (string?)null));

        if (updated == 0)
            return new DeviceClaimResult { Success = false, Message = "Mã liên kết không hợp lệ hoặc đã được sử dụng." };

        return new DeviceClaimResult { Success = true, Message = "Đã thêm thiết bị vào tài khoản của bạn." };
    }

    public Task AddAsync(Device device) => _devices.AddAsync(device);
    public void Remove(Device device) => _devices.Delete(device);
    public async Task SaveChangesAsync() => await _devices.SaveChangesAsync();

    private static string NormalizeDeviceName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("Tên thiết bị là bắt buộc.", nameof(deviceName));

        return Regex.Replace(deviceName.Trim(), "\\s+", " ");
    }

    private static string BuildDeviceCodeSlug(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator || builder.Length == 0)
                continue;

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('-');
    }
}
