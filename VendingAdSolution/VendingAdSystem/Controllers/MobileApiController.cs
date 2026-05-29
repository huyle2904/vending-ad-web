using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Filters;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/mobile")]
public class MobileApiController : ControllerBase
{
    private readonly IMobilePlaybackService _mobilePlaybackService;
    private readonly IDeviceCredentialService _deviceCredentialService;
    private readonly IDeviceService _deviceService;
    private readonly ITimeService _timeService;

    public MobileApiController(
        IMobilePlaybackService mobilePlaybackService,
        IDeviceCredentialService deviceCredentialService,
        IDeviceService deviceService,
        ITimeService timeService)
    {
        _mobilePlaybackService = mobilePlaybackService;
        _deviceCredentialService = deviceCredentialService;
        _deviceService = deviceService;
        _timeService = timeService;
    }

    [HttpPost("devices/register")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "TÃªn thiáº¿t bá»‹ lÃ  báº¯t buá»™c." });

        var normalizedDeviceName = request.DeviceName.Trim();
        if (normalizedDeviceName.Length > 100)
            return BadRequest(new { message = "TÃªn thiáº¿t bá»‹ khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ 100 kÃ½ tá»±." });

        var utcNow = _timeService.UtcNow;
        var deviceSecret = _deviceCredentialService.GenerateSecret();
        var device = new Device
        {
            DeviceCode = await _deviceService.GenerateDeviceCodeAsync(normalizedDeviceName),
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

    [HttpGet("devices/{deviceCode}")]
    [MobileRateLimit(MobileRateLimitPolicy.DeviceInfo)]
    public async Task<IActionResult> GetDevice(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        if (!await IsDeviceAuthenticatedAsync(deviceCode))
            return Unauthorized(new { message = "Thông tin xác thực thiết bị không hợp lệ." });

        var response = await _mobilePlaybackService.GetDeviceAsync(deviceCode);
        if (response == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        return Ok(response);
    }

    [HttpPost("heartbeat")]
    [MobileRateLimit(MobileRateLimitPolicy.Heartbeat)]
    public async Task<IActionResult> Heartbeat([FromBody] MobileHeartbeatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        if (!await IsDeviceAuthenticatedAsync(request.DeviceCode))
            return Unauthorized(new { message = "Thông tin xác thực thiết bị không hợp lệ." });

        var response = await _mobilePlaybackService.HeartbeatAsync(request.DeviceCode);
        if (response == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        return Ok(response);
    }

    [HttpGet("playback-state/{deviceCode}")]
    [MobileRateLimit(MobileRateLimitPolicy.PlaybackState)]
    public async Task<IActionResult> GetPlaybackState(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

        if (!await IsDeviceAuthenticatedAsync(deviceCode))
            return Unauthorized(new { message = "Thông tin xác thực thiết bị không hợp lệ." });

        var response = await _mobilePlaybackService.GetPlaybackStateAsync(deviceCode);
        if (response == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        return Ok(response);
    }

    private Task<bool> IsDeviceAuthenticatedAsync(string deviceCode)
    {
        return _deviceCredentialService.ValidateSecretAsync(deviceCode, GetDeviceSecret());
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
