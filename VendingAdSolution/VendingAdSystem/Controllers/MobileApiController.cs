using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Filters;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/mobile")]
public class MobileApiController : ControllerBase
{
    private readonly IMobilePlaybackService _mobilePlaybackService;

    public MobileApiController(IMobilePlaybackService mobilePlaybackService)
    {
        _mobilePlaybackService = mobilePlaybackService;
    }

    [HttpGet("devices/{deviceCode}")]
    public async Task<IActionResult> GetDevice(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { message = "Mã thiết bị là bắt buộc." });

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

        var response = await _mobilePlaybackService.GetPlaybackStateAsync(deviceCode);
        if (response == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        return Ok(response);
    }
}
