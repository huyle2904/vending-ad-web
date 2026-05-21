using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IMobilePlaybackCacheService
{
    string PlaybackStateKey(string deviceCode);
    string DeviceActiveScheduleKey(string deviceCode);
    string ScheduleContentKey(int scheduleId, string version);
    Task<MobileScheduleContentCache> GetOrBuildScheduleContentAsync(PlaybackSchedule schedule, DateTime vietnamNow);
    Task WarmScheduleContentAsync(int scheduleId);
    Task InvalidateScheduleDevicesAsync(int scheduleId);
    Task InvalidateDevicePlaybackCachesAsync(IEnumerable<string> deviceCodes);
}

public class MobilePlaybackCacheService : IMobilePlaybackCacheService
{
    private static readonly TimeSpan ScheduleContentTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(5);
    private readonly ICacheService _cache;
    private readonly IRepository<PlaybackSchedule> _schedules;
    private readonly ITimeService _timeService;
    private readonly IApplicationMetrics _metrics;

    public MobilePlaybackCacheService(ICacheService cache, IRepository<PlaybackSchedule> schedules, ITimeService timeService, IApplicationMetrics metrics)
    {
        _cache = cache;
        _schedules = schedules;
        _timeService = timeService;
        _metrics = metrics;
    }

    public string PlaybackStateKey(string deviceCode) => $"mobile:playback-state:{deviceCode}";
    public string DeviceActiveScheduleKey(string deviceCode) => $"mobile:device-active-schedule:{deviceCode}";
    public string ScheduleContentKey(int scheduleId, string version) => $"mobile:schedule-content:{scheduleId}:{version}";

    public async Task<MobileScheduleContentCache> GetOrBuildScheduleContentAsync(PlaybackSchedule schedule, DateTime vietnamNow)
    {
        // This cache belongs to the schedule, not a single device. Devices that run
        // the same schedule/version can reuse one shared payload instead of loading
        // schedule items and media from the database 200 times.
        var orderedItems = schedule.Items.OrderBy(i => i.OrderIndex).ThenBy(i => i.Id).ToList();
        var version = BuildScheduleVersion(schedule, orderedItems);
        var key = ScheduleContentKey(schedule.Id, version);
        var cached = await _cache.GetAsync<MobileScheduleContentCache>(key);
        if (cached != null)
        {
            _metrics.RecordCacheHit("schedule_content");
            return cached;
        }

        _metrics.RecordCacheMiss("schedule_content");

        var lockKey = $"lock:{key}";
        var lockToken = Guid.NewGuid().ToString("N");
        var lockTaken = await _cache.TryAcquireLockAsync(lockKey, lockToken, LockTtl);

        try
        {
            if (!lockTaken)
            {
                // Another request is already building this schedule cache. Wait briefly
                // and read again so we avoid a cache stampede against the database.
                await Task.Delay(120);
                cached = await _cache.GetAsync<MobileScheduleContentCache>(key);
                if (cached != null)
                {
                    _metrics.RecordCacheHit("schedule_content");
                    return cached;
                }
            }

            var content = BuildScheduleContent(schedule, orderedItems, version, vietnamNow);
            await _cache.SetAsync(key, content, ScheduleContentTtl);
            return content;
        }
        finally
        {
            if (lockTaken)
                await _cache.ReleaseLockAsync(lockKey, lockToken);
        }
    }

    public async Task WarmScheduleContentAsync(int scheduleId)
    {
        PlaybackSchedule? schedule;
        using (_metrics.ObserveDatabaseQuery("warm_schedule_content"))
        {
            schedule = await _schedules.Query()
                .AsNoTracking()
                .Include(s => s.Items).ThenInclude(i => i.Media)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.IsActive);
        }

        if (schedule == null)
            return;

        await GetOrBuildScheduleContentAsync(schedule, _timeService.ToVietnamTime(_timeService.UtcNow));
    }

    public async Task InvalidateScheduleDevicesAsync(int scheduleId)
    {
        // When a schedule changes, every attached device must stop using its old
        // per-device playback cache and resolve the new schedule version on next poll.
        List<string> deviceCodes;
        using (_metrics.ObserveDatabaseQuery("invalidate_schedule_devices"))
        {
            deviceCodes = await _schedules.Query()
                .AsNoTracking()
                .Where(s => s.Id == scheduleId)
                .SelectMany(s => s.Devices.Select(d => d.Device.DeviceCode))
                .ToListAsync();
        }

        await InvalidateDevicePlaybackCachesAsync(deviceCodes);
    }

    public async Task InvalidateDevicePlaybackCachesAsync(IEnumerable<string> deviceCodes)
    {
        foreach (var deviceCode in deviceCodes
                     .Where(code => !string.IsNullOrWhiteSpace(code))
                     .Select(code => code.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await _cache.RemoveAsync(PlaybackStateKey(deviceCode));
            await _cache.RemoveAsync(DeviceActiveScheduleKey(deviceCode));
        }
    }

    private MobileScheduleContentCache BuildScheduleContent(PlaybackSchedule schedule, List<PlaybackScheduleItem> orderedItems, string version, DateTime vietnamNow)
    {
        return new MobileScheduleContentCache
        {
            Schedule = new MobileScheduleResponse
            {
                Id = schedule.Id,
                Name = schedule.Name,
                Version = version,
                IsImmediate = schedule.IsImmediate,
                StartDateUtc = schedule.StartDate,
                EndDateUtc = schedule.EndDate,
                StartTime = schedule.StartTime.ToString(@"hh\:mm\:ss"),
                EndTime = schedule.EndTime.ToString(@"hh\:mm\:ss"),
                PlaybackAnchorUtc = GetPlaybackAnchorUtc(schedule, vietnamNow)
            },
            Items = orderedItems.Select(i => new MobilePlaybackItemResponse
            {
                MediaId = i.MediaId,
                FileName = i.Media.FileName,
                FileUrl = i.Media.FileUrl,
                OrderIndex = i.OrderIndex,
                FileSize = i.Media.FileSize,
                Checksum = null,
                DurationSeconds = i.Media.DurationSeconds
            }).ToList()
        };
    }

    private DateTime GetPlaybackAnchorUtc(PlaybackSchedule schedule, DateTime vietnamNow)
    {
        if (schedule.IsImmediate && schedule.ImmediateStartedAt.HasValue)
            return schedule.ImmediateStartedAt.Value;

        return _timeService.ToUtc(vietnamNow.Date.Add(schedule.StartTime));
    }

    private static string BuildScheduleVersion(PlaybackSchedule schedule, List<PlaybackScheduleItem> orderedItems)
    {
        var itemVersion = string.Join("-", orderedItems.Select(i => $"{i.Id}:{i.MediaId}:{i.OrderIndex}"));
        return $"{schedule.Id}-{schedule.CreatedAt.Ticks}-{itemVersion}";
    }
}
