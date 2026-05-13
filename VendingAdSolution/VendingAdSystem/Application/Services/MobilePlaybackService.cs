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

    public MobilePlaybackService(
        IRepository<Device> devices,
        IRepository<PlaybackSchedule> playbackSchedules,
        ITimeService timeService,
        ICacheService cacheService)
    {
        _devices = devices;
        _playbackSchedules = playbackSchedules;
        _timeService = timeService;
        _cacheService = cacheService;
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
        device.LastSeen = utcNow;
        await _devices.SaveChangesAsync();

        return new MobileHeartbeatResponse
        {
            DeviceCode = device.DeviceCode,
            ServerTimeUtc = utcNow,
            LastSeen = device.LastSeen
        };
    }

    private static string BuildPlaybackCacheKey(string deviceCode) => $"mobile:playback-state:{deviceCode}";

    public async Task<MobilePlaybackStateResponse?> GetPlaybackStateAsync(string deviceCode)
    {
        var normalizedCode = NormalizeDeviceCode(deviceCode);
        var cacheKey = BuildPlaybackCacheKey(normalizedCode);
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

        var orderedItems = schedule.Items
            .OrderBy(i => i.OrderIndex)
            .ThenBy(i => i.Id)
            .ToList();

        response.HasActiveSchedule = true;
        response.Schedule = new MobileScheduleResponse
        {
            Id = schedule.Id,
            Name = schedule.Name,
            Version = BuildScheduleVersion(schedule, orderedItems),
            IsImmediate = schedule.IsImmediate,
            StartDateUtc = schedule.StartDate,
            EndDateUtc = schedule.EndDate,
            StartTime = schedule.StartTime.ToString(@"hh\:mm\:ss"),
            EndTime = schedule.EndTime.ToString(@"hh\:mm\:ss"),
            PlaybackAnchorUtc = GetPlaybackAnchorUtc(schedule, vietnamNow)
        };
        response.Items = orderedItems.Select(i => new MobilePlaybackItemResponse
        {
            MediaId = i.MediaId,
            FileName = i.Media.FileName,
            FileUrl = i.Media.FileUrl,
            OrderIndex = i.OrderIndex,
            FileSize = i.Media.FileSize,
            Checksum = null,
            DurationSeconds = i.Media.DurationSeconds
        }).ToList();

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

    private DateTime GetPlaybackAnchorUtc(PlaybackSchedule schedule, DateTime vietnamNow)
    {
        if (schedule.IsImmediate && schedule.ImmediateStartedAt.HasValue)
            return schedule.ImmediateStartedAt.Value;

        var vietnamAnchor = vietnamNow.Date.Add(schedule.StartTime);
        return _timeService.ToUtc(vietnamAnchor);
    }

    private static string BuildScheduleVersion(PlaybackSchedule schedule, List<PlaybackScheduleItem> orderedItems)
    {
        var itemVersion = string.Join("-", orderedItems.Select(i => $"{i.Id}:{i.MediaId}:{i.OrderIndex}"));
        return $"{schedule.Id}-{schedule.CreatedAt.Ticks}-{itemVersion}";
    }

    private static string NormalizeDeviceCode(string deviceCode)
    {
        return deviceCode.Trim();
    }
}
