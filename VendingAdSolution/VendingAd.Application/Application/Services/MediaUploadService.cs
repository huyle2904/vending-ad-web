using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    private readonly IConfiguration _configuration;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMediaService _mediaService;
    private readonly ITimeService _timeService;
    private readonly IPlaylistManagementService _playlistManagementService;
    private readonly IRepository<PlaylistItem> _playlistItems;
    private readonly IRepository<PlaybackScheduleItem> _scheduleItems;
    private readonly IAuditService _auditService;
    private readonly ILogger<MediaUploadService> _logger;

    public MediaUploadService(IConfiguration configuration, IFileStorageService fileStorageService, IMediaService mediaService, ITimeService timeService, IPlaylistManagementService playlistManagementService, IRepository<PlaylistItem> playlistItems, IRepository<PlaybackScheduleItem> scheduleItems, IAuditService auditService, ILogger<MediaUploadService> logger)
    {
        _configuration = configuration;
        _fileStorageService = fileStorageService;
        _mediaService = mediaService;
        _timeService = timeService;
        _playlistManagementService = playlistManagementService;
        _playlistItems = playlistItems;
        _scheduleItems = scheduleItems;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<UploadVideoResult> UploadAsync(UploadVideoRequest request, string scheme, HostString host)
    {
        var validation = await ValidateVideoFileAsync(request.File);
        if (!validation.Success)
            return new UploadVideoResult { Success = false, Message = validation.Message };

        var storedMedia = await CreateStoredMediaAsync(request.File!, request.UserId, validation.Extension);
        if (!storedMedia.Success)
            return new UploadVideoResult { Success = false, Message = storedMedia.Message };

        var media = storedMedia.Media!;
        try
        {
            await _mediaService.AddAsync(media);
            await _mediaService.SaveChangesAsync();
            await _auditService.LogAsync(new AuditLogEntry
            {
                Action = AuditActions.UploadVideo,
                TargetType = AuditTargets.Media,
                TargetId = media.Id,
                Details = new
                {
                    media.FileName,
                    media.UserId,
                    media.FileSize,
                    media.DurationSeconds
                }
            });

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
            await CleanupStoredMediaAsync(media);
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

        var videos = await _mediaService.GetUserMediaByIdsAsync(ids, userId);

        if (videos.Count != ids.Count)
            return new PlaylistActionResult { Success = false, Message = "Không tìm thấy video" };

        foreach (var video in videos)
        {
            await _fileStorageService.DeleteAsync(video.FileUrl);
            _mediaService.Remove(video);
        }

        await _mediaService.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.DeleteVideo,
            TargetType = videos.Count == 1 ? AuditTargets.Media : AuditTargets.MediaBatch,
            TargetId = videos.Count == 1 ? videos[0].Id : null,
            Details = new
            {
                UserId = userId,
                DeletedVideos = videos.Select(video => new
                {
                    video.Id,
                    video.FileName
                }).ToList()
            }
        });
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

        var storedMedia = await CreateStoredMediaAsync(uploadFile, userId, validation.Extension);
        if (!storedMedia.Success)
            return new UploadVideoResult { Success = false, Message = storedMedia.Message };

        var media = storedMedia.Media!;
        try
        {
            await _mediaService.AddAsync(media);
            await _mediaService.SaveChangesAsync();

            await _playlistManagementService.AddMediaToPlaylistAsync(playlist.Id, media.Id, userId);
            await _auditService.LogAsync(new AuditLogEntry
            {
                Action = AuditActions.UploadVideo,
                TargetType = AuditTargets.Media,
                TargetId = media.Id,
                Details = new
                {
                    media.FileName,
                    media.UserId,
                    media.FileSize,
                    media.DurationSeconds,
                    PlaylistId = playlist.Id,
                    PlaylistName = playlist.Name
                }
            });

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
            await CleanupStoredMediaAsync(media);
            throw;
        }
    }

    private async Task<StoredMediaResult> CreateStoredMediaAsync(IFormFile uploadFile, int userId, string extension)
    {
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using (var stream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await uploadFile.CopyToAsync(stream);
            }

            var probeResult = await ValidateWithFfprobeAsync(tempFilePath);
            if (!probeResult.Success)
                return StoredMediaResult.Invalid(probeResult.Message);

            var storedFile = await _fileStorageService.SaveAsync(tempFilePath, uniqueName);

            return StoredMediaResult.Valid(new Media
            {
                FileName = Path.GetFileName(uploadFile.FileName),
                FileUrl = storedFile.FileUrl,
                FileSize = uploadFile.Length,
                DurationSeconds = probeResult.DurationSeconds,
                UserId = userId,
                UploadedAt = _timeService.UtcNow
            });
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
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

    private async Task<VideoProbeResult> ValidateWithFfprobeAsync(string filePath)
    {
        if (!_configuration.GetValue("VideoValidation:FfprobeEnabled", true))
            return VideoProbeResult.Valid(null);

        var ffprobePath = _configuration["VideoValidation:FfprobePath"];
        if (string.IsNullOrWhiteSpace(ffprobePath))
            ffprobePath = "ffprobe";

        var requireFfprobe = _configuration.GetValue("VideoValidation:RequireFfprobe", false);
        var timeoutSeconds = Math.Max(1, _configuration.GetValue("VideoValidation:ProbeTimeoutSeconds", 10));

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.ArgumentList.Add("error");
            process.StartInfo.ArgumentList.Add("-show_entries");
            process.StartInfo.ArgumentList.Add("stream=codec_type,codec_name,duration:format=duration");
            process.StartInfo.ArgumentList.Add("-of");
            process.StartInfo.ArgumentList.Add("json");
            process.StartInfo.ArgumentList.Add(filePath);

            if (!process.Start())
                return requireFfprobe
                    ? VideoProbeResult.Invalid("Không thể chạy ffprobe để kiểm tra video.")
                    : VideoProbeResult.Valid(null);

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                KillProcess(process);
                return VideoProbeResult.Invalid("Kiểm tra video bằng ffprobe bị quá thời gian.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe rejected uploaded video. ExitCode={ExitCode}, Error={Error}", process.ExitCode, stderr);
                return VideoProbeResult.Invalid("File không phải video hợp lệ hoặc không đọc được metadata.");
            }

            return ParseFfprobeOutput(stdout);
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe executable was not found or could not be started.");
            return requireFfprobe
                ? VideoProbeResult.Invalid("ffprobe chưa được cài đặt hoặc không chạy được.")
                : VideoProbeResult.Valid(null);
        }
    }

    private VideoProbeResult ParseFfprobeOutput(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            JsonElement? videoStream = null;

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (GetString(stream, "codec_type").Equals("video", StringComparison.OrdinalIgnoreCase))
                    {
                        videoStream = stream;
                        break;
                    }
                }
            }

            if (videoStream == null)
                return VideoProbeResult.Invalid("File không có video stream hợp lệ.");

            var codecName = GetString(videoStream.Value, "codec_name");
            if (!IsAllowedVideoCodec(codecName))
                return VideoProbeResult.Invalid($"Video codec '{codecName}' chưa được hỗ trợ.");

            var duration = TryGetDuration(videoStream.Value);
            if (duration == null && root.TryGetProperty("format", out var format))
                duration = TryGetDuration(format);

            if (duration == null || duration.Value <= TimeSpan.Zero)
                return VideoProbeResult.Invalid("Không đọc được duration hợp lệ từ video.");

            return VideoProbeResult.Valid((int)Math.Ceiling(duration.Value.TotalSeconds));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ffprobe returned invalid JSON.");
            return VideoProbeResult.Invalid("Không đọc được metadata video từ ffprobe.");
        }
    }

    private bool IsAllowedVideoCodec(string codecName)
    {
        if (string.IsNullOrWhiteSpace(codecName))
            return false;

        var configuredCodecs = _configuration.GetSection("VideoValidation:AllowedVideoCodecs")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        var allowedCodecs = configuredCodecs.Any()
            ? configuredCodecs
            : new List<string> { "h264", "hevc", "vp8", "vp9", "av1" };

        return allowedCodecs.Contains(codecName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static TimeSpan? TryGetDuration(JsonElement element)
    {
        var value = GetString(element, "duration");
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return null;

        return TimeSpan.FromSeconds(seconds);
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup after probe timeout.
        }
    }

    private static void TryDeleteTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Best-effort cleanup for staged upload files.
        }
    }

    private async Task CleanupStoredMediaAsync(Media media)
    {
        try
        {
            await _fileStorageService.DeleteAsync(media.FileUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up stored file '{FileUrl}' after upload failure.", media.FileUrl);
        }

        if (media.Id <= 0)
            return;

        try
        {
            _mediaService.Remove(media);
            await _mediaService.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to roll back media record {MediaId} after upload failure.", media.Id);
        }
    }

    private sealed record VideoFileValidationResult(bool Success, string Message, string Extension)
    {
        public static VideoFileValidationResult Valid(string extension) => new(true, string.Empty, extension);
        public static VideoFileValidationResult Invalid(string message) => new(false, message, string.Empty);
    }

    private sealed record StoredMediaResult(bool Success, string Message, Media? Media)
    {
        public static StoredMediaResult Valid(Media media) => new(true, string.Empty, media);
        public static StoredMediaResult Invalid(string message) => new(false, message, null);
    }

    private sealed record VideoProbeResult(bool Success, string Message, int? DurationSeconds)
    {
        public static VideoProbeResult Valid(int? durationSeconds) => new(true, string.Empty, durationSeconds);
        public static VideoProbeResult Invalid(string message) => new(false, message, null);
    }
}
