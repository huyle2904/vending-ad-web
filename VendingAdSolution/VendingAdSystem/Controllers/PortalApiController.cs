using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/portal")]
public class PortalApiController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ITimeService _timeService;
    private readonly IMediaUploadService _mediaUploadService;
    private readonly IPlaylistService _playlistService;

    public PortalApiController(IDeviceService deviceService, ITimeService timeService, IMediaUploadService mediaUploadService, IPlaylistService playlistService)
    {
        _deviceService = deviceService;
        _timeService = timeService;
        _mediaUploadService = mediaUploadService;
        _playlistService = playlistService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50MB max
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] int userId)
    {
        var sessionUserId = HttpContext.Session.GetInt32("UserId");
        if (sessionUserId == null || sessionUserId != userId)
            return Unauthorized(new { message = "Invalid user session" });

        var result = await _mediaUploadService.UploadAsync(new UploadVideoRequest
        {
            File = file,
            UserId = userId
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
    public async Task<IActionResult> GetDevices()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return Unauthorized();

        var devices = await _deviceService.Query()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.DeviceCode)
            .Select(d => new { id = d.Id, code = d.DeviceCode, location = d.Location })
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("playlist/{deviceCode}")]
    public async Task<IActionResult> GetPlaylist(string deviceCode)
    {
        var items = await _playlistService.GetPlaylistAsync(deviceCode);

        if (items == null || !items.Any())
            return NotFound(new { message = $"No active schedule assigned to device '{deviceCode}'." });

        return Ok(items);
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceCode))
            return BadRequest(new { message = "DeviceCode is required." });

        var device = await _deviceService.Query()
            .FirstOrDefaultAsync(d => d.DeviceCode == req.DeviceCode);

        if (device == null)
            return NotFound(new { message = "Device not found." });

        device.LastSeen = _timeService.UtcNow;
        await _deviceService.SaveChangesAsync();

        return Ok(new { message = "ok", timestamp = device.LastSeen });
    }

    [HttpPost("devices/register")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCode))
            return BadRequest(new { message = "DeviceCode is required." });

        var deviceCode = request.DeviceCode.Trim();
        var existing = await _deviceService.GetByCodeAsync(deviceCode);
        if (existing != null)
            return Conflict(new { message = "DeviceCode already exists." });

        var device = new Device
        {
            DeviceCode = deviceCode,
            UserId = null,
            IsActive = true,
            LastSeen = _timeService.UtcNow
        };

        await _deviceService.AddAsync(device);
        await _deviceService.SaveChangesAsync();

        return Ok(new
        {
            message = "registered",
            device.Id,
            device.DeviceCode,
            device.IsActive,
            device.LastSeen
        });
    }
}

public record HeartbeatRequest(string DeviceCode);
public record RegisterDeviceRequest(string DeviceCode);
