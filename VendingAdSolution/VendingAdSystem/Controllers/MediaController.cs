using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IDeviceService _deviceService;
    private readonly IMediaService _mediaService;
    private readonly ICampaignService _campaignService;

    public MediaController(IWebHostEnvironment env, IDeviceService deviceService, IMediaService mediaService, ICampaignService campaignService)
    {
        _env = env;
        _deviceService = deviceService;
        _mediaService = mediaService;
        _campaignService = campaignService;
    }

    /// <summary>
    /// POST /api/media/upload
    /// Form fields: file (video), deviceCode (string)
    /// Saves video to wwwroot/uploads, creates Media + Campaign records.
    /// Replaces any existing campaign for that device.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)] // 500 MB max
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string deviceCode)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { message = "Device code is required." });

        // Ensure device exists
        var device = await _deviceService.GetByCodeAsync(deviceCode);
        if (device == null)
        {
            device = new Device { DeviceCode = deviceCode };
            await _deviceService.AddAsync(device);
            await _deviceService.SaveChangesAsync();
        }

        // Save file to wwwroot/uploads
        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath   = Path.Combine(uploadsPath, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var media = new Media
        {
            FileName = file.FileName,
            FileUrl  = $"{baseUrl}/uploads/{uniqueName}"
        };
        await _mediaService.AddAsync(media);
        await _mediaService.SaveChangesAsync();

        // Replace existing campaign assignment for this device
        var existing = _campaignService.Query().Where(c => c.DeviceId == device.Id);
        _campaignService.RemoveRange(existing);
        await _campaignService.AddAsync(new Campaign { DeviceId = device.Id, MediaId = media.Id });
        await _campaignService.SaveChangesAsync();

        return Ok(new { media.FileUrl, media.FileName, deviceCode });
    }
}
