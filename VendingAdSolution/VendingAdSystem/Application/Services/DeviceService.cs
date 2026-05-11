using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IDeviceService
{
    IQueryable<Device> Query();
    Task<Device?> GetByIdAsync(int id);
    Task<Device?> GetByCodeAsync(string deviceCode);
    Task<string> GenerateClaimCodeAsync();
    Task<DeviceClaimResult> ClaimAsync(string claimCode, int userId, DateTime utcNow);
    Task AddAsync(Device device);
    void Remove(Device device);
    Task SaveChangesAsync();
}

public class DeviceService : IDeviceService
{
    private readonly IRepository<Device> _devices;

    public DeviceService(IRepository<Device> devices)
    {
        _devices = devices;
    }

    public IQueryable<Device> Query() => _devices.Query();
    public Task<Device?> GetByIdAsync(int id) => _devices.GetByIdAsync(id);
    public Task<Device?> GetByCodeAsync(string deviceCode) => _devices.Query().FirstOrDefaultAsync(d => d.DeviceCode == deviceCode);
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
}
