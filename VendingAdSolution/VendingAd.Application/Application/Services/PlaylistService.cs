using VendingAdSystem.Application.DTOs;

namespace VendingAdSystem.Application.Services;

public interface IPlaylistService
{
    Task<List<PlaylistResponse>?> GetPlaylistAsync(string deviceCode);
}

public class PlaylistService : IPlaylistService
{
    private readonly ITimeService _timeService;
    private readonly IPlaybackScheduleResolver _scheduleResolver;

    public PlaylistService(ITimeService timeService, IPlaybackScheduleResolver scheduleResolver)
    {
        _timeService = timeService;
        _scheduleResolver = scheduleResolver;
    }

    public async Task<List<PlaylistResponse>?> GetPlaylistAsync(string deviceCode)
    {
        var schedule = await _scheduleResolver.ResolveCurrentForDeviceCodeAsync(deviceCode, _timeService.UtcNow);

        if (schedule == null)
            return null;

        return schedule.Items
            .OrderBy(i => i.OrderIndex)
            .Select(i => new PlaylistResponse
            {
                FileUrl = NormalizeMediaUrl(i.Media.FileUrl),
                FileName = i.Media.FileName,
                OrderIndex = i.OrderIndex
            })
            .ToList();
    }

    private static string NormalizeMediaUrl(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return fileUrl;

        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var absoluteUri) && absoluteUri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return absoluteUri.AbsolutePath + absoluteUri.Query;

        return fileUrl;
    }
}
