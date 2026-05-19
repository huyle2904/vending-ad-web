using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Filters;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/mobile")]
public class MobileApiController : ControllerBase
{
    private readonly IMobilePlaybackService _mobilePlaybackService;
    private readonly IDeviceCredentialService _deviceCredentialService;

    public MobileApiController(IMobilePlaybackService mobilePlaybackService, IDeviceCredentialService deviceCredentialService)
    {
        _mobilePlaybackService = mobilePlaybackService;
        _deviceCredentialService = deviceCredentialService;
    }

    [HttpGet("devices/{deviceCode}")]
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
