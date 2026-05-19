using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IMobilePlaybackService
{
    Task<MobileDeviceResponse?> GetDeviceAsync(string deviceCode);
    Task<MobileHeartbeatResponse?> HeartbeatAsync(string deviceCode);
    Task<MobilePlaybackStateResponse?> GetPlaybackStateAsync(string deviceCode);
}

public class MobilePlaybackService : IMobilePlaybackService
{
    private readonly IRepository<Device> _devices;
    private readonly IRepository<PlaybackSchedule> _playbackSchedules;
    private readonly ITimeService _timeService;
    private readonly ICacheService _cacheService;
    private readonly IMobilePlaybackCacheService _playbackCache;
    private readonly IDevicePresenceService _devicePresence;

    public MobilePlaybackService(
        IRepository<Device> devices,
        IRepository<PlaybackSchedule> playbackSchedules,
        ITimeService timeService,
        ICacheService cacheService,
        IMobilePlaybackCacheService playbackCache,
        IDevicePresenceService devicePresence)
    {
        _devices = devices;
        _playbackSchedules = playbackSchedules;
        _timeService = timeService;
        _cacheService = cacheService;
        _playbackCache = playbackCache;
        _devicePresence = devicePresence;
    }

    public async Task<MobileDeviceResponse?> GetDeviceAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        var device = await _devices.Query()
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);

        return device == null ? null : ToDeviceResponse(device);
    }

    public async Task<MobileHeartbeatResponse?> HeartbeatAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        var device = await _devices.Query()
            .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);

        if (device == null)
            return null;

        var utcNow = _timeService.UtcNow;
        await _devicePresence.MarkOnlineAsync(device.DeviceCode, utcNow);

        if (_devicePresence.ShouldUpdateLastSeen(device.LastSeen, utcNow))
        {
            device.LastSeen = utcNow;
            await _devices.SaveChangesAsync();
        }

        return new MobileHeartbeatResponse
        {
            DeviceCode = device.DeviceCode,
            ServerTimeUtc = utcNow,
            LastSeen = device.LastSeen
        };
    }

    public async Task<MobilePlaybackStateResponse?> GetPlaybackStateAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        var cacheKey = _playbackCache.PlaybackStateKey(normalizedCode);
        var cached = await _cacheService.GetAsync<MobilePlaybackStateResponse>(cacheKey);
        if (cached != null)
            return cached;

        var device = await _devices.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);

        if (device == null)
            return null;

        var utcNow = _timeService.UtcNow;
        var response = CreateEmptyPlaybackState(device, utcNow);

        if (!device.IsActive || device.UserId == null)
        {
            var unclaimedOrInactiveTtl = device.UserId == null
                ? TimeSpan.FromSeconds(30)
                : TimeSpan.FromSeconds(10);
            await _cacheService.SetAsync(cacheKey, response, unclaimedOrInactiveTtl);
            return response;
        }

        var vietnamNow = _timeService.ToVietnamTime(utcNow);
        var currentTime = vietnamNow.TimeOfDay;
        var activeScheduleKey = _playbackCache.DeviceActiveScheduleKey(normalizedCode);
        var candidates = await _playbackSchedules.Query()
            .AsNoTracking()
            .Include(s => s.Devices).ThenInclude(d => d.Device)
            .Include(s => s.Items).ThenInclude(i => i.Media)
            .Where(s => s.IsActive)
            .Where(s => s.StartDate <= utcNow && s.EndDate >= utcNow)
            .Where(s => s.Devices.Any(d => d.DeviceId == device.Id))
            .ToListAsync();

        var schedule = candidates
            .Where(s => IsScheduleActiveNow(s, utcNow, currentTime))
            .OrderByDescending(s => s.IsImmediate)
            .ThenByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.StartDate)
            .FirstOrDefault();

        if (schedule == null)
        {
            await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromSeconds(10));
            return response;
        }

        var scheduleContent = await _playbackCache.GetOrBuildScheduleContentAsync(schedule, vietnamNow);
        // Store the device -> schedule/version mapping separately. Later requests can
        // use this lightweight mapping before reading shared schedule content.
        await _cacheService.SetAsync(activeScheduleKey, new MobileDeviceScheduleCache
        {
            ScheduleId = scheduleContent.Schedule.Id,
            Version = scheduleContent.Schedule.Version,
            HasActiveSchedule = true,
            ResolvedAtUtc = utcNow
        }, TimeSpan.FromSeconds(10));

        response.HasActiveSchedule = true;
        response.Schedule = scheduleContent.Schedule;
        response.Items = scheduleContent.Items;

        await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromSeconds(5));

        return response;
    }

    private MobilePlaybackStateResponse CreateEmptyPlaybackState(Device device, DateTime utcNow)
    {
        var claimRequired = device.UserId == null;
        return new MobilePlaybackStateResponse
        {
            DeviceCode = device.DeviceCode,
            ServerTimeUtc = utcNow,
            HasActiveSchedule = false,
            ClaimRequired = claimRequired,
            ClaimCode = claimRequired ? device.ClaimCode : null
        };
    }

    private MobileDeviceResponse ToDeviceResponse(Device device)
    {
        var claimRequired = device.UserId == null;
        return new MobileDeviceResponse
        {
            DeviceCode = device.DeviceCode,
            Location = device.Location,
            IsActive = device.IsActive,
            ClaimRequired = claimRequired,
            ClaimCode = claimRequired ? device.ClaimCode : null,
            ClaimedAt = device.ClaimedAt,
            LastSeen = device.LastSeen,
            AssignedUser = device.User == null ? null : new MobileAssignedUserResponse
            {
                Id = device.User.Id,
                Username = device.User.Username,
                Email = device.User.Email,
                FullName = device.User.FullName
            }
        };
    }

    private static bool IsScheduleActiveNow(PlaybackSchedule schedule, DateTime utcNow, TimeSpan currentTime)
    {
        if (!schedule.IsImmediate)
            return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;

        if (schedule.ImmediateStartedAt.HasValue && schedule.ImmediateStartedAt.Value.Date == utcNow.Date)
            return currentTime <= schedule.EndTime;

        return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;
    }

    private static string NormalizeDeviceCode(string deviceCode)
    {
        return deviceCode.Trim();
    }
}
