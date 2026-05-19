using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Filters;

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

    public PortalApiController(IDeviceService deviceService, ITimeService timeService, IMediaUploadService mediaUploadService, IPlaylistService playlistService, IDevicePresenceService devicePresenceService, ICurrentSession currentSession, IDeviceCredentialService deviceCredentialService)
    {
        _deviceService = deviceService;
        _timeService = timeService;
        _mediaUploadService = mediaUploadService;
        _playlistService = playlistService;
        _devicePresenceService = devicePresenceService;
        _currentSession = currentSession;
        _deviceCredentialService = deviceCredentialService;
    }

    [HttpPost("upload")]
    [Authorize(Roles = "User")]
    [RequestSizeLimit(52_428_800)] // 50 MiB max
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized(new { message = "Invalid user session" });

        var result = await _mediaUploadService.UploadAsync(new UploadVideoRequest
        {
            File = file,
            UserId = userId.Value
        }, Request.Scheme, Request.Host);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { 
            message = result.Message,
            fileName = result.FileName,
            fileUrl = result.FileUrl
        });
    }

    [HttpGet("devices")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetDevices()
    {
        var userId = _currentSession.UserId;
        if (userId == null)
            return Unauthorized();

        var devices = await _deviceService.Query()
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.DeviceCode)
            .Select(d => new { id = d.Id, code = d.DeviceCode, location = d.Location })
            .ToListAsync();

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

        var device = await _deviceService.Query()
            .FirstOrDefaultAsync(d => d.DeviceCode == req.DeviceCode);

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
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        var deviceCode = request.DeviceCode.Trim();
        var existing = await _deviceService.GetByCodeAsync(deviceCode);
        if (existing != null)
            return Conflict(new { message = "DeviceCode already exists." });

        var utcNow = _timeService.UtcNow;
        var deviceSecret = _deviceCredentialService.GenerateSecret();
        var device = new Device
        {
            DeviceCode = deviceCode,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            ClaimCode = await _deviceService.GenerateClaimCodeAsync(),
            UserId = null,
            IsActive = true,
            LastSeen = utcNow
        };
        _deviceCredentialService.AssignSecret(device, deviceSecret, utcNow);

        await _deviceService.AddAsync(device);
        await _deviceService.SaveChangesAsync();

        return Ok(new
        {
            message = "registered",
            device.Id,
            device.DeviceCode,
            device.Location,
            device.ClaimCode,
            DeviceSecret = deviceSecret,
            device.IsActive,
            device.LastSeen
        });
    }

    private async Task<bool> CanAccessDeviceEndpointAsync(string deviceCode)
    {
        var normalizedCode = deviceCode.Trim();
        var userId = _currentSession.UserId;
        if (userId.HasValue)
        {
            var ownsDevice = await _deviceService.Query()
                .AsNoTracking()
                .AnyAsync(d => d.DeviceCode == normalizedCode && d.UserId == userId.Value && d.IsActive);

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
public record RegisterDeviceRequest(string DeviceCode, string? Location);
