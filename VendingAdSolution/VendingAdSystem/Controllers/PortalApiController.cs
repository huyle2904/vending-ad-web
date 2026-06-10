using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Filters;
using VendingAdSystem.Hubs;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/portal")]
public class PortalApiController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ITimeService _timeService;
    private readonly IMediaUploadService _mediaUploadService;
    private readonly IPlaylistService _playlistService;
    private readonly IDevicePresenceService _devicePresenceService;
    private readonly ICurrentSession _currentSession;
    private readonly IDeviceCredentialService _deviceCredentialService;
    private readonly IMobilePlaybackService _mobilePlaybackService;
    private readonly IHubContext<DeviceStatusHub> _hubContext;

    public PortalApiController(
        IDeviceService deviceService,
        ITimeService timeService,
        IMediaUploadService mediaUploadService,
        IPlaylistService playlistService,
        IDevicePresenceService devicePresenceService,
        ICurrentSession currentSession,
        IDeviceCredentialService deviceCredentialService,
        IMobilePlaybackService mobilePlaybackService,
        IHubContext<DeviceStatusHub> hubContext)
    {
        _deviceService = deviceService;
        _timeService = timeService;
        _mediaUploadService = mediaUploadService;
        _playlistService = playlistService;
        _devicePresenceService = devicePresenceService;
        _currentSession = currentSession;
        _deviceCredentialService = deviceCredentialService;
        _mobilePlaybackService = mobilePlaybackService;
        _hubContext = hubContext;
    }

    [HttpPost("upload")]
    [Authorize(Roles = "User")]
    [RequestSizeLimit(52_953_088)] // 50 MiB video + 512 KiB thumbnail
    [RequestFormLimits(MultipartBodyLengthLimit = 52_953_088)]
    public async Task<IActionResult> Upload(IFormFile? file, IFormFile? thumbnail)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized(new { message = "Invalid user session" });

        var result = await _mediaUploadService.UploadAsync(new UploadVideoRequest
        {
            File = file,
            Thumbnail = thumbnail,
            UserId = userId.Value
        }, Request.Scheme, Request.Host);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new {
            message = result.Message,
            fileName = result.FileName,
            fileUrl = result.FileUrl,
            thumbnailUrl = result.ThumbnailUrl
        });
    }

    [HttpGet("devices")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetDevices()
    {
        var userId = _currentSession.UserId;
        if (userId == null)
            return Unauthorized();

        var devices = await _deviceService.GetUserDevicesAsync(userId.Value);
        var result = devices.Select(d => new { id = d.Id, code = d.DeviceCode, name = d.DeviceName, location = d.Location });

        return Ok(devices);
    }

    [HttpGet("playlist/{deviceCode}")]
    [MobileRateLimit(MobileRateLimitPolicy.Playlist)]
    public async Task<IActionResult> GetPlaylist(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        if (!await CanAccessDeviceEndpointAsync(deviceCode))
            return Unauthorized(new { message = "Không có quyền truy cập thiết bị." });

        var items = await _playlistService.GetPlaylistAsync(deviceCode);

        if (items == null || !items.Any())
            return NotFound(new { message = $"Không có lịch phát đang hoạt động cho thiết bị '{deviceCode}'." });

        return Ok(items);
    }

    [HttpPost("heartbeat")]
    [MobileRateLimit(MobileRateLimitPolicy.Heartbeat)]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        if (!await CanAccessDeviceEndpointAsync(req.DeviceCode))
            return Unauthorized(new { message = "Không có quyền truy cập thiết bị." });

        var device = await _deviceService.GetByCodeAsync(req.DeviceCode);

        if (device == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var utcNow = _timeService.UtcNow;
        await _devicePresenceService.MarkOnlineAsync(device.DeviceCode, utcNow);

        if (_devicePresenceService.ShouldUpdateLastSeen(device.LastSeen, utcNow))
        {
            device.LastSeen = utcNow;
            await _deviceService.SaveChangesAsync();
        }

        return Ok(new { message = "ok", timestamp = device.LastSeen });
    }

    [HttpPost("devices/register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "Tên thiết bị là bắt buộc." });

        var normalizedDeviceName = request.DeviceName.Trim();
        if (normalizedDeviceName.Length > 100)
            return BadRequest(new { message = "Tên thiết bị không được vượt quá 100 ký tự." });

        var deviceCode = await _deviceService.GenerateDeviceCodeAsync(normalizedDeviceName);
        var utcNow = _timeService.UtcNow;
        var deviceSecret = _deviceCredentialService.GenerateSecret();
        var device = new Device
        {
            DeviceCode = deviceCode,
            DeviceName = normalizedDeviceName,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            ClaimCode = await _deviceService.GenerateClaimCodeAsync(),
            UserId = null,
            IsActive = true,
            LastSeen = utcNow
        };
        _deviceCredentialService.AssignSecret(device, deviceSecret, utcNow);

        await _deviceService.AddAsync(device);
        await _deviceService.SaveChangesAsync();

        return Ok(new RegisterDeviceResponseDto
        {
            Id = device.Id,
            DeviceCode = device.DeviceCode,
            DeviceName = device.DeviceName,
            Location = device.Location,
            ClaimCode = device.ClaimCode,
            DeviceSecret = deviceSecret,
            IsActive = device.IsActive,
            LastSeen = device.LastSeen
        });
    }

    [HttpPost("devices/{deviceCode}/force-online")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> ForceOnline(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        var userId = _currentSession.UserId;
        if (userId == null || !await _deviceService.IsDeviceOwnedByUserAsync(deviceCode.Trim(), userId.Value))
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var response = await _mobilePlaybackService.ForceOnlineAsync(deviceCode);
        if (response == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        // Broadcast mode change to dashboard via SignalR
        await _hubContext.Clients.Group("dashboard").SendAsync("DeviceStatusUpdated", new
        {
            deviceCode = response.DeviceCode,
            playbackMode = "Online",
            currentFileName = (string?)null,
            isOnline = true,
            lastSeen = (DateTime?)null,
            serverTimeUtc = DateTime.UtcNow
        });

        return Ok(response);
    }

    private async Task<bool> CanAccessDeviceEndpointAsync(string deviceCode)
    {
        var normalizedCode = deviceCode.Trim();
        var userId = _currentSession.UserId;
        if (userId.HasValue)
        {
            var ownsDevice = await _deviceService.IsDeviceOwnedByUserAsync(normalizedCode, userId.Value);

            if (ownsDevice)
                return true;
        }

        return await _deviceCredentialService.ValidateSecretAsync(normalizedCode, GetDeviceSecret());
    }

    private string? GetDeviceSecret()
    {
        if (Request.Headers.TryGetValue("X-Device-Secret", out StringValues secretHeader))
            return secretHeader.FirstOrDefault();

        var authorization = Request.Headers.Authorization.FirstOrDefault();
        const string bearerPrefix = "Bearer ";
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[bearerPrefix.Length..];
        }

        return null;
    }
}

public record HeartbeatRequest(string DeviceCode);
