using System.Text.RegularExpressions;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IYouTubeService
{
    Task<(bool Success, string Message)> AddYouTubeLinkAsync(string url, int userId);
    Task<List<Media>> GetUserYouTubeLinksAsync(int userId);
    Task<bool> DeleteYouTubeLinkAsync(int id, int userId);
    bool TryValidateYouTubeUrl(string url, out string? normalizedUrl);
}

public class YouTubeService : IYouTubeService
{
    private readonly IMediaService _mediaService;
    private readonly ITimeService _timeService;

    public YouTubeService(IMediaService mediaService, ITimeService timeService)
    {
        _mediaService = mediaService;
        _timeService = timeService;
    }

    public async Task<(bool Success, string Message)> AddYouTubeLinkAsync(string url, int userId)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "Vui lòng nhập link YouTube.");

        url = url.Trim();

        if (!TryValidateYouTubeUrl(url, out var normalizedUrl))
            return (false, "Link YouTube không hợp lệ. Vui lòng kiểm tra lại.");

        var videoId = ExtractVideoId(normalizedUrl!);
        var fileName = $"YouTube - {videoId}";

        var existing = await _mediaService.GetUserMediaAsync(userId);
        if (existing.Any(m => m.MediaType == MediaType.YouTube && m.FileUrl == normalizedUrl))
            return (false, "Link YouTube này đã được thêm vào thư viện.");

        var media = new Media
        {
            FileName = fileName,
            FileUrl = normalizedUrl,
            FileSize = 0,
            DurationSeconds = null,
            ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg",
            MediaType = MediaType.YouTube,
            UploadedAt = _timeService.UtcNow,
            UserId = userId
        };

        await _mediaService.AddAsync(media);
        await _mediaService.SaveChangesAsync();

        return (true, $"Đã thêm YouTube video vào thư viện.");
    }

    public async Task<List<Media>> GetUserYouTubeLinksAsync(int userId)
    {
        var allMedia = await _mediaService.GetUserMediaAsync(userId);
        return allMedia.Where(m => m.MediaType == MediaType.YouTube)
                       .OrderByDescending(m => m.UploadedAt)
                       .ToList();
    }

    public async Task<bool> DeleteYouTubeLinkAsync(int id, int userId)
    {
        var media = await _mediaService.GetByIdAsync(id);
        if (media == null || media.UserId != userId)
            return false;

        _mediaService.Remove(media);
        await _mediaService.SaveChangesAsync();
        return true;
    }

    public bool TryValidateYouTubeUrl(string url, out string? normalizedUrl)
    {
        normalizedUrl = null;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Handle various YouTube URL formats
        var patterns = new[]
        {
            // youtube.com/watch?v=VIDEO_ID
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/watch\?.*v=(?<id>[a-zA-Z0-9_-]{11})",
            // youtu.be/VIDEO_ID
            @"(?:https?:\/\/)?youtu\.be\/(?<id>[a-zA-Z0-9_-]{11})",
            // youtube.com/embed/VIDEO_ID
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/embed\/(?<id>[a-zA-Z0-9_-]{11})",
            // youtube.com/shorts/VIDEO_ID
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/shorts\/(?<id>[a-zA-Z0-9_-]{11})",
            // youtube.com/v/VIDEO_ID
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/v\/(?<id>[a-zA-Z0-9_-]{11})",
            // m.youtube.com/watch?v=VIDEO_ID
            @"(?:https?:\/\/)?m\.youtube\.com\/watch\?.*v=(?<id>[a-zA-Z0-9_-]{11})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var videoId = match.Groups["id"].Value;
                normalizedUrl = $"https://www.youtube.com/watch?v={videoId}";
                return true;
            }
        }

        return false;
    }

    private static string? ExtractVideoId(string normalizedUrl)
    {
        var match = Regex.Match(normalizedUrl, @"v=(?<id>[a-zA-Z0-9_-]{11})");
        return match.Success ? match.Groups["id"].Value : null;
    }
}
