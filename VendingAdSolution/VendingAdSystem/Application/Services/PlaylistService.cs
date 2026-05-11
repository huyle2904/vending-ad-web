using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Application.Services;

public interface IPlaylistService
{
    Task<List<PlaylistResponse>?> GetPlaylistAsync(string deviceCode);
}

public class PlaylistService : IPlaylistService
{
    private readonly ITimeService _timeService;
    private readonly IPlaybackScheduleService _playbackScheduleService;

    public PlaylistService(ITimeService timeService, IPlaybackScheduleService playbackScheduleService)
    {
        _timeService = timeService;
        _playbackScheduleService = playbackScheduleService;
    }

    public async Task<List<PlaylistResponse>?> GetPlaylistAsync(string deviceCode)
    {
        var utcNow = _timeService.UtcNow;
        var vietnamNow = _timeService.ToVietnamTime(utcNow);
        var currentTime = vietnamNow.TimeOfDay;

        var schedules = await _playbackScheduleService.GetAllAsync();
        var schedule = schedules
            .Where(s => s.IsActive)
            .Where(s => s.StartDate <= utcNow && s.EndDate >= utcNow)
            .Where(s => s.Devices.Any(d => d.Device.DeviceCode == deviceCode))
            .Where(s => IsScheduleActiveNow(s, utcNow, currentTime))
            .OrderByDescending(s => s.IsImmediate)
            .ThenByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.StartDate)
            .FirstOrDefault();

        if (schedule == null)
            return null;

        return schedule.Items
            .OrderBy(i => i.OrderIndex)
            .Select(i => new PlaylistResponse
            {
                FileUrl = i.Media.FileUrl,
                FileName = i.Media.FileName,
                OrderIndex = i.OrderIndex
            })
            .ToList();
    }

    private bool IsScheduleActiveNow(PlaybackSchedule schedule, DateTime utcNow, TimeSpan currentTime)
    {
        if (!schedule.IsImmediate)
            return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;

        if (schedule.ImmediateStartedAt.HasValue && schedule.ImmediateStartedAt.Value.Date == utcNow.Date)
            return currentTime <= schedule.EndTime;

        return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;
    }
}
