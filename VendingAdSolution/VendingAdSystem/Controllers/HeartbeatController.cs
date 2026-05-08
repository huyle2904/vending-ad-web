using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api")]
public class HeartbeatController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public HeartbeatController(IDeviceService deviceService) => _deviceService = deviceService;

    /// <summary>
    /// POST /api/heartbeat
    /// Body: { "deviceCode": "TABLET-001" }
    /// Updates LastSeen for the device. Auto-registers unknown devices.
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceCode))
            return BadRequest(new { message = "DeviceCode is required." });

        var device = await _deviceService.Query()
            .FirstOrDefaultAsync(d => d.DeviceCode == req.DeviceCode);

        if (device == null)
        {
            device = new Device { DeviceCode = req.DeviceCode };
            await _deviceService.AddAsync(device);
        }

        device.LastSeen = DateTime.UtcNow;
        await _deviceService.SaveChangesAsync();

        return Ok(new { message = "ok", timestamp = device.LastSeen });
    }
}

public record HeartbeatRequest(string DeviceCode);
