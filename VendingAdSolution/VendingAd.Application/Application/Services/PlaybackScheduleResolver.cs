using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IPlaybackScheduleResolver
{
    Task<PlaybackSchedule?> ResolveCurrentForDeviceAsync(int deviceId, DateTime utcNow);
    Task<PlaybackSchedule?> ResolveCurrentForDeviceCodeAsync(string deviceCode, DateTime utcNow);
    PlaybackSchedule? ResolveCurrentForDevice(IEnumerable<PlaybackSchedule> schedules, int deviceId, DateTime utcNow);
    PlaybackSchedule? ResolveUpcomingForDevice(IEnumerable<PlaybackSchedule> schedules, int deviceId, DateTime utcNow, int? excludingScheduleId = null);
    DateTime? GetNextScheduleStartUtc(PlaybackSchedule schedule, DateTime utcNow);
}

public sealed class PlaybackScheduleResolver : IPlaybackScheduleResolver
{
    private readonly IRepository<PlaybackSchedule> _playbackSchedules;
    private readonly ITimeService _timeService;

    public PlaybackScheduleResolver(IRepository<PlaybackSchedule> playbackSchedules, ITimeService timeService)
    {
        _playbackSchedules = playbackSchedules;
        _timeService = timeService;
    }

    public async Task<PlaybackSchedule?> ResolveCurrentForDeviceAsync(int deviceId, DateTime utcNow)
    {
        var candidates = await QueryCurrentCandidates(utcNow)
            .Where(s => s.Devices.Any(d => d.DeviceId == deviceId))
            .ToListAsync();

        return ResolveCurrent(candidates, utcNow);
    }

    public async Task<PlaybackSchedule?> ResolveCurrentForDeviceCodeAsync(string deviceCode, DateTime utcNow)
    {
        var normalizedCode = deviceCode.Trim();
        var candidates = await QueryCurrentCandidates(utcNow)
            .Where(s => s.Devices.Any(d => d.Device.DeviceCode == normalizedCode))
            .ToListAsync();

        return ResolveCurrent(candidates, utcNow);
    }

    public PlaybackSchedule? ResolveCurrentForDevice(IEnumerable<PlaybackSchedule> schedules, int deviceId, DateTime utcNow)
    {
        return ResolveCurrent(
            schedules
                .Where(s => s.IsActive)
                .Where(s => s.StartDate <= utcNow && s.EndDate >= utcNow)
                .Where(s => s.Devices.Any(d => d.DeviceId == deviceId)),
            utcNow);
    }

    public PlaybackSchedule? ResolveUpcomingForDevice(IEnumerable<PlaybackSchedule> schedules, int deviceId, DateTime utcNow, int? excludingScheduleId = null)
    {
        return schedules
            .Where(s => s.IsActive)
            .Where(s => s.Devices.Any(d => d.DeviceId == deviceId))
            .Where(s => excludingScheduleId == null || s.Id != excludingScheduleId.Value)
            .Select(s => new { Schedule = s, NextStart = GetNextScheduleStartUtc(s, utcNow) })
            .Where(x => x.NextStart.HasValue)
            .OrderBy(x => x.NextStart)
            .ThenBy(x => x.Schedule.StartTime)
            .Select(x => x.Schedule)
            .FirstOrDefault();
    }

    public DateTime? GetNextScheduleStartUtc(PlaybackSchedule schedule, DateTime utcNow)
    {
        var vietnamNow = _timeService.ToVietnamTime(utcNow);
        var startDate = GetVietnamDate(schedule.StartDate);
        var endDate = GetVietnamDate(schedule.EndDate);

        if (vietnamNow.Date < startDate)
            return _timeService.ToUtc(startDate.Add(schedule.StartTime));

        if (vietnamNow.Date > endDate)
            return null;

        if (schedule.StartTime > vietnamNow.TimeOfDay)
            return _timeService.ToUtc(vietnamNow.Date.Add(schedule.StartTime));

        var nextDate = vietnamNow.Date.AddDays(1);
        if (nextDate <= endDate)
            return _timeService.ToUtc(nextDate.Add(schedule.StartTime));

        return null;
    }

    private IQueryable<PlaybackSchedule> QueryCurrentCandidates(DateTime utcNow)
    {
        return _playbackSchedules.Query()
            .AsNoTracking()
            .Include(s => s.Devices).ThenInclude(d => d.Device)
            .Include(s => s.Items).ThenInclude(i => i.Media)
            .Where(s => s.IsActive)
            .Where(s => s.StartDate <= utcNow && s.EndDate >= utcNow);
    }

    private PlaybackSchedule? ResolveCurrent(IEnumerable<PlaybackSchedule> candidates, DateTime utcNow)
    {
        var currentTime = _timeService.ToVietnamTime(utcNow).TimeOfDay;

        return candidates
            .Where(s => IsScheduleActiveNow(s, utcNow, currentTime))
            .OrderByDescending(s => s.IsImmediate)
            .ThenByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.StartDate)
            .FirstOrDefault();
    }

    private bool IsScheduleActiveNow(PlaybackSchedule schedule, DateTime utcNow, TimeSpan currentTime)
    {
        if (!schedule.IsImmediate)
            return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;

        if (schedule.ImmediateStartedAt.HasValue && schedule.ImmediateStartedAt.Value.Date == utcNow.Date)
            return currentTime <= schedule.EndTime;

        return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;
    }

    private DateTime GetVietnamDate(DateTime utcDate)
    {
        return _timeService.ToVietnamTime(utcDate).Date;
    }
}
