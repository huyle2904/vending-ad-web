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
    private const long MaxUploadBytes = 50 * 1024 * 1024;
    private static readonly Dictionary<string, string[]> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = new[] { "video/mp4", "application/mp4" },
        [".mov"] = new[] { "video/quicktime" },
        [".webm"] = new[] { "video/webm" }
    };

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
        var validation = await ValidateVideoFileAsync(request.File);
        if (!validation.Success)
            return new UploadVideoResult { Success = false, Message = validation.Message };

        var uploadFile = request.File!;
        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid():N}{validation.Extension}";
        var filePath = Path.Combine(uploadsPath, uniqueName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Create);
            await uploadFile.CopyToAsync(stream);

            var media = new Media
            {
                FileName = Path.GetFileName(uploadFile.FileName),
                FileUrl = $"/uploads/{uniqueName}",
                FileSize = uploadFile.Length,
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
        var validation = await ValidateVideoFileAsync(file);
        if (!validation.Success)
            return new UploadVideoResult { Success = false, Message = validation.Message };

        var uploadFile = file!;
        var playlist = await _playlistManagementService.GetPlaylistForUserAsync(playlistId, userId);
        if (playlist == null)
            return new UploadVideoResult { Success = false, Message = "Không tìm thấy danh sách phát" };

        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid():N}{validation.Extension}";
        var filePath = Path.Combine(uploadsPath, uniqueName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Create);
            await uploadFile.CopyToAsync(stream);

            var media = new Media
            {
                FileName = Path.GetFileName(uploadFile.FileName),
                FileUrl = $"/uploads/{uniqueName}",
                FileSize = uploadFile.Length,
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

    private static async Task<VideoFileValidationResult> ValidateVideoFileAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return VideoFileValidationResult.Invalid("No file provided");

        if (file.Length > MaxUploadBytes)
            return VideoFileValidationResult.Invalid("File size must be less than 50MB");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedVideoContentTypes.ContainsKey(extension))
            return VideoFileValidationResult.Invalid("Only .mp4, .mov, and .webm video files are allowed");

        if (!IsAllowedContentType(extension, file.ContentType))
            return VideoFileValidationResult.Invalid("Video content type is not allowed");

        if (!await HasAllowedVideoSignatureAsync(file, extension))
            return VideoFileValidationResult.Invalid("File content does not match an allowed video format");

        return VideoFileValidationResult.Valid(extension);
    }

    private static bool IsAllowedContentType(string extension, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) ||
            contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AllowedVideoContentTypes[extension].Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> HasAllowedVideoSignatureAsync(IFormFile file, string extension)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[16];
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));

        return extension switch
        {
            ".mp4" or ".mov" => bytesRead >= 8
                && header[4] == (byte)'f'
                && header[5] == (byte)'t'
                && header[6] == (byte)'y'
                && header[7] == (byte)'p',
            ".webm" => bytesRead >= 4
                && header[0] == 0x1A
                && header[1] == 0x45
                && header[2] == 0xDF
                && header[3] == 0xA3,
            _ => false
        };
    }

    private string GetUploadsPath()
    {
        var configuredPath = _configuration["UploadsPath"];
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(_env.WebRootPath, "uploads")
            : configuredPath;
    }

    private sealed record VideoFileValidationResult(bool Success, string Message, string Extension)
    {
        public static VideoFileValidationResult Valid(string extension) => new(true, string.Empty, extension);
        public static VideoFileValidationResult Invalid(string message) => new(false, message, string.Empty);
    }
}
