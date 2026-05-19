using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IMediaUploadService
{
    Task<UploadVideoResult> UploadAsync(UploadVideoRequest request, string scheme, HostString host);
    Task<UploadVideoResult> UploadToPlaylistAsync(int playlistId, int userId, IFormFile? file, string scheme, HostString host);
    Task<bool> DeleteVideoAsync(int videoId, int userId);
    Task<PlaylistActionResult> DeleteVideosAsync(IEnumerable<int> videoIds, int userId);
}

public class MediaUploadService : IMediaUploadService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IMediaService _mediaService;
    private readonly ITimeService _timeService;
    private readonly IPlaylistManagementService _playlistManagementService;
    private readonly IRepository<PlaylistItem> _playlistItems;
    private readonly IRepository<PlaybackScheduleItem> _scheduleItems;

    public MediaUploadService(IWebHostEnvironment env, IConfiguration configuration, IMediaService mediaService, ITimeService timeService, IPlaylistManagementService playlistManagementService, IRepository<PlaylistItem> playlistItems, IRepository<PlaybackScheduleItem> scheduleItems)
    {
        _env = env;
        _configuration = configuration;
        _mediaService = mediaService;
        _timeService = timeService;
        _playlistManagementService = playlistManagementService;
        _playlistItems = playlistItems;
        _scheduleItems = scheduleItems;
    }

    public async Task<UploadVideoResult> UploadAsync(UploadVideoRequest request, string scheme, HostString host)
    {
        if (request.File == null || request.File.Length == 0)
            return new UploadVideoResult { Success = false, Message = "No file provided" };

        if (request.File.Length > 50 * 1024 * 1024)
            return new UploadVideoResult { Success = false, Message = "File size must be less than 50MB" };

        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
        var filePath = Path.Combine(uploadsPath, uniqueName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Create);
            await request.File.CopyToAsync(stream);

            var media = new Media
            {
                FileName = request.File.FileName,
                FileUrl = $"{scheme}://{host}/uploads/{uniqueName}",
                FileSize = request.File.Length,
                UserId = request.UserId,
                UploadedAt = _timeService.UtcNow
            };

            await _mediaService.AddAsync(media);
            await _mediaService.SaveChangesAsync();

            return new UploadVideoResult
            {
                Success = true,
                Message = "Đã tải lên video",
                FileName = media.FileName,
                FileUrl = media.FileUrl
            };
        }
        catch
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            throw;
        }
    }

    public async Task<bool> DeleteVideoAsync(int videoId, int userId)
    {
        var result = await DeleteVideosAsync(new[] { videoId }, userId);
        return result.Success;
    }

    public async Task<PlaylistActionResult> DeleteVideosAsync(IEnumerable<int> videoIds, int userId)
    {
        var ids = videoIds.Distinct().ToList();
        if (!ids.Any())
            return new PlaylistActionResult { Success = false, Message = "No video selected." };

        var usedInSchedules = await _scheduleItems.Query()
            .Include(i => i.Media)
            .Where(i => ids.Contains(i.MediaId))
            .Select(i => i.Media.FileName)
            .Distinct()
            .ToListAsync();

        if (usedInSchedules.Any())
            return new PlaylistActionResult { Success = false, Message = $"Không thể xóa video đang dùng trong schedule: {string.Join(", ", usedInSchedules)}." };

        var usedInPlaylists = await _playlistItems.Query()
            .Include(i => i.Media)
            .Include(i => i.Playlist)
            .Where(i => ids.Contains(i.MediaId))
            .Select(i => $"{i.Media.FileName} ({i.Playlist.Name})")
            .Distinct()
            .ToListAsync();

        if (usedInPlaylists.Any())
            return new PlaylistActionResult { Success = false, Message = $"Không thể xóa video đang nằm trong playlist: {string.Join(", ", usedInPlaylists)}." };

        foreach (var videoId in ids)
        {
            var video = await _mediaService.Query()
                .FirstOrDefaultAsync(m => m.Id == videoId && m.UserId == userId);

            if (video == null)
                return new PlaylistActionResult { Success = false, Message = "Không tìm thấy video" };

            var filePathPart = Uri.TryCreate(video.FileUrl, UriKind.Absolute, out var fileUri)
                ? fileUri.LocalPath
                : video.FileUrl;
            var fileName = Path.GetFileName(filePathPart);
            var filePath = Path.Combine(GetUploadsPath(), fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            _mediaService.Remove(video);
        }

        await _mediaService.SaveChangesAsync();
        return new PlaylistActionResult { Success = true, Message = ids.Count == 1 ? "Đã xóa video" : "Đã xóa các video" };
    }

    public async Task<UploadVideoResult> UploadToPlaylistAsync(int playlistId, int userId, IFormFile? file, string scheme, HostString host)
    {
        if (file == null || file.Length == 0)
            return new UploadVideoResult { Success = false, Message = "No file provided" };

        if (file.Length > 50 * 1024 * 1024)
            return new UploadVideoResult { Success = false, Message = "File size must be less than 50MB" };

        var playlist = await _playlistManagementService.GetPlaylistForUserAsync(playlistId, userId);
        if (playlist == null)
            return new UploadVideoResult { Success = false, Message = "Không tìm thấy danh sách phát" };

        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, uniqueName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var media = new Media
            {
                FileName = file.FileName,
                FileUrl = $"{scheme}://{host}/uploads/{uniqueName}",
                FileSize = file.Length,
                UserId = userId,
                UploadedAt = _timeService.UtcNow
            };

            await _mediaService.AddAsync(media);
            await _mediaService.SaveChangesAsync();

            await _playlistManagementService.AddMediaToPlaylistAsync(playlist.Id, media.Id, userId);

            return new UploadVideoResult
            {
                Success = true,
                Message = "Đã thêm video vào danh sách phát",
                FileName = media.FileName,
                FileUrl = media.FileUrl,
                PlaylistId = playlist.Id,
                PlaylistName = playlist.Name,
                DeviceCount = 0
            };
        }
        catch
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            throw;
        }
    }

    private string GetUploadsPath()
    {
        var configuredPath = _configuration["UploadsPath"];
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(_env.WebRootPath, "uploads")
            : configuredPath;
    }
}
