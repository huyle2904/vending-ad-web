using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IMobilePlaybackService
{
    Task<MobileDeviceResponse?> GetDeviceAsync(string deviceCode);
    Task<MobileHeartbeatResponse?> HeartbeatAsync(string deviceCode, string? currentFileName = null, string? playbackMode = null);
    Task<MobilePlaybackStateResponse?> GetPlaybackStateAsync(string deviceCode);
    Task<MobileSetPlaybackModeResponse?> SetPlaybackModeAsync(string deviceCode, string mode, string? localFileName = null, DateTime? localFileStartedUtc = null);
    Task<MobileSetPlaybackModeResponse?> ForceOnlineAsync(string deviceCode);
}

public class MobilePlaybackCacheOptions
{
    public int PlaybackStateTtlSeconds { get; set; } = 20;
    public int NoActiveScheduleTtlSeconds { get; set; } = 10;
    public int InactiveDeviceTtlSeconds { get; set; } = 20;
    public int UnclaimedDeviceTtlSeconds { get; set; } = 60;
    public int DeviceActiveScheduleTtlSeconds { get; set; } = 20;
}

public class MobilePlaybackService : IMobilePlaybackService
{
    private readonly IRepository<Device> _devices;
    private readonly IPlaybackScheduleResolver _scheduleResolver;
    private readonly ITimeService _timeService;
    private readonly ICacheService _cacheService;
    private readonly IMobilePlaybackCacheService _playbackCache;
    private readonly IDevicePresenceService _devicePresence;
    private readonly IApplicationMetrics _metrics;
    private readonly MobilePlaybackCacheOptions _cacheOptions;

    public MobilePlaybackService(
        IRepository<Device> devices,
        IPlaybackScheduleResolver scheduleResolver,
        ITimeService timeService,
        ICacheService cacheService,
        IMobilePlaybackCacheService playbackCache,
        IDevicePresenceService devicePresence,
        IApplicationMetrics metrics,
        IOptions<MobilePlaybackCacheOptions> cacheOptions)
    {
        _devices = devices;
        _scheduleResolver = scheduleResolver;
        _timeService = timeService;
        _cacheService = cacheService;
        _playbackCache = playbackCache;
        _devicePresence = devicePresence;
        _metrics = metrics;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<MobileDeviceResponse?> GetDeviceAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        Device? device;
        using (_metrics.ObserveDatabaseQuery("mobile_get_device"))
        {
            device = await _devices.Query()
                .AsNoTracking()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);
        }

        return device == null ? null : ToDeviceResponse(device);
    }

    public async Task<MobileHeartbeatResponse?> HeartbeatAsync(string deviceCode, string? currentFileName = null, string? playbackMode = null)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        Device? device;
        using (_metrics.ObserveDatabaseQuery("mobile_heartbeat_lookup"))
        {
            device = await _devices.Query()
                .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);
        }

        if (device == null)
            return null;

        var utcNow = _timeService.UtcNow;
        await _devicePresence.MarkOnlineAsync(device.DeviceCode, utcNow);

        if (_devicePresence.ShouldUpdateLastSeen(device.LastSeen, utcNow))
        {
            device.LastSeen = utcNow;
        }

        // Update real-time status from heartbeat
        if (currentFileName != null)
        {
            device.CurrentFileName = currentFileName;
        }
        if (playbackMode != null)
        {
            device.PlaybackMode = playbackMode;
        }

        await _devices.SaveChangesAsync();

        var forceOnline = device.PlaybackMode == "Online" && device.LocalFileName != null;
        if (forceOnline)
        {
            device.LocalFileName = null;
            device.LocalFileStartedUtc = null;
            await _devices.SaveChangesAsync();
        }

        return new MobileHeartbeatResponse
        {
            DeviceCode = device.DeviceCode,
            ServerTimeUtc = utcNow,
            LastSeen = device.LastSeen,
            PlaybackMode = device.PlaybackMode,
            CurrentFileName = device.CurrentFileName,
            ForceOnline = forceOnline ? true : null
        };
    }

    public async Task<MobilePlaybackStateResponse?> GetPlaybackStateAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        var cacheKey = _playbackCache.PlaybackStateKey(normalizedCode);
        var cached = await _cacheService.GetAsync<MobilePlaybackStateResponse>(cacheKey);
        if (cached != null)
        {
            _metrics.RecordCacheHit("playback_state");
            return cached;
        }

        _metrics.RecordCacheMiss("playback_state");

        Device? device;
        using (_metrics.ObserveDatabaseQuery("mobile_playback_device_lookup"))
        {
            device = await _devices.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);
        }

        if (device == null)
            return null;

        var utcNow = _timeService.UtcNow;
        var response = CreateEmptyPlaybackState(device, utcNow);

        if (!device.IsActive || device.UserId == null)
        {
            var unclaimedOrInactiveTtl = device.UserId == null
                ? CacheTtl(_cacheOptions.UnclaimedDeviceTtlSeconds)
                : CacheTtl(_cacheOptions.InactiveDeviceTtlSeconds);
            await _cacheService.SetAsync(cacheKey, response, unclaimedOrInactiveTtl);
            return response;
        }

        var vietnamNow = _timeService.ToVietnamTime(utcNow);
        var activeScheduleKey = _playbackCache.DeviceActiveScheduleKey(normalizedCode);
        PlaybackSchedule? schedule;
        using (_metrics.ObserveDatabaseQuery("mobile_playback_schedule_candidates"))
        {
            schedule = await _scheduleResolver.ResolveCurrentForDeviceAsync(device.Id, utcNow);
        }

        if (schedule == null)
        {
            await _cacheService.SetAsync(cacheKey, response, CacheTtl(_cacheOptions.NoActiveScheduleTtlSeconds));
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
        }, CacheTtl(_cacheOptions.DeviceActiveScheduleTtlSeconds));

        response.HasActiveSchedule = true;
        response.Schedule = scheduleContent.Schedule;
        response.Items = scheduleContent.Items;

        await _cacheService.SetAsync(cacheKey, response, CacheTtl(_cacheOptions.PlaybackStateTtlSeconds));

        return response;
    }

    public async Task<MobileSetPlaybackModeResponse?> SetPlaybackModeAsync(string deviceCode, string mode, string? localFileName = null, DateTime? localFileStartedUtc = null)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        Device? device;
        using (_metrics.ObserveDatabaseQuery("mobile_set_playback_mode"))
        {
            device = await _devices.Query()
                .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);
        }

        if (device == null)
            return null;

        device.PlaybackMode = mode;
        device.LocalFileName = mode == "Local" ? localFileName : null;
        device.LocalFileStartedUtc = mode == "Local" ? localFileStartedUtc : null;
        await _devices.SaveChangesAsync();

        // Invalidate playback state cache so next poll picks up the change
        var cacheKey = _playbackCache.PlaybackStateKey(normalizedCode);
        await _cacheService.RemoveAsync(cacheKey);

        return new MobileSetPlaybackModeResponse
        {
            DeviceCode = device.DeviceCode,
            PlaybackMode = device.PlaybackMode,
            Message = mode == "Local" ? "Switched to local playback" : "Switched to online schedule"
        };
    }

    public async Task<MobileSetPlaybackModeResponse?> ForceOnlineAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        Device? device;
        using (_metrics.ObserveDatabaseQuery("mobile_force_online"))
        {
            device = await _devices.Query()
                .FirstOrDefaultAsync(d => d.DeviceCode == normalizedCode);
        }

        if (device == null)
            return null;

        device.PlaybackMode = "Online";
        device.LocalFileName = null;
        device.LocalFileStartedUtc = null;
        await _devices.SaveChangesAsync();

        var cacheKey = _playbackCache.PlaybackStateKey(normalizedCode);
        await _cacheService.RemoveAsync(cacheKey);

        return new MobileSetPlaybackModeResponse
        {
            DeviceCode = device.DeviceCode,
            PlaybackMode = "Online",
            Message = "Device forced back to online mode"
        };
    }

    private MobilePlaybackStateResponse CreateEmptyPlaybackState(Device device, DateTime utcNow)
    {
        var claimRequired = device.UserId == null;
        return new MobilePlaybackStateResponse
        {
            DeviceCode = device.DeviceCode,
            DeviceName = device.DeviceName,
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
            DeviceName = device.DeviceName,
            Location = device.Location,
            IsActive = device.IsActive,
            ClaimRequired = claimRequired,
            ClaimCode = claimRequired ? device.ClaimCode : null,
            ClaimedAt = device.ClaimedAt,
            LastSeen = device.LastSeen,
            PlaybackMode = device.PlaybackMode,
            LocalFileName = device.LocalFileName,
            LocalFileStartedUtc = device.LocalFileStartedUtc,
            AssignedUser = device.User == null ? null : new MobileAssignedUserResponse
            {
                Id = device.User.Id,
                Username = device.User.Username,
                Email = device.User.Email,
                FullName = device.User.FullName
            }
        };
    }

    private static string NormalizeDeviceCode(string deviceCode)
    {
        return deviceCode.Trim();
    }

    private static TimeSpan CacheTtl(int seconds)
    {
        return TimeSpan.FromSeconds(Math.Max(1, seconds));
    }
}

