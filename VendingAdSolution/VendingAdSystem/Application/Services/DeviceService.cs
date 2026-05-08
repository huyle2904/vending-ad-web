using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IDeviceService
{
    IQueryable<Device> Query();
    Task<Device?> GetByIdAsync(int id);
    Task<Device?> GetByCodeAsync(string deviceCode);
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
    public Task AddAsync(Device device) => _devices.AddAsync(device);
    public void Remove(Device device) => _devices.Delete(device);
    public async Task SaveChangesAsync() => await _devices.SaveChangesAsync();
}
