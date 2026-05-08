using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/portal")]
public class PortalApiController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IMediaService _mediaService;
    private readonly IDeviceService _deviceService;
    private readonly ICampaignService _campaignService;

    public PortalApiController(IWebHostEnvironment env, IMediaService mediaService, IDeviceService deviceService, ICampaignService campaignService)
    {
        _env = env;
        _mediaService = mediaService;
        _deviceService = deviceService;
        _campaignService = campaignService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50MB max
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] int userId, [FromForm] string startDate, [FromForm] string endDate, [FromForm] List<int> deviceIds)
    {
        var sessionUserId = HttpContext.Session.GetInt32("UserId");
        if (sessionUserId == null || sessionUserId != userId)
            return Unauthorized(new { message = "Invalid user session" });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (file.Length > 50 * 1024 * 1024)
            return BadRequest(new { message = "File size must be less than 50MB" });

        if (deviceIds == null || !deviceIds.Any())
            return BadRequest(new { message = "Please select at least one device" });

        if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            return BadRequest(new { message = "Please select start and end dates" });

        var startDateTime = DateTime.Parse(startDate);
        var endDateTime = DateTime.Parse(endDate);

        if (endDateTime <= startDateTime)
            return BadRequest(new { message = "End date must be after start date" });

        // Save file
        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var media = new Media
        {
            FileName = file.FileName,
            FileUrl = $"{baseUrl}/uploads/{uniqueName}",
            FileSize = file.Length,
            UserId = userId,
            UploadedAt = DateTime.UtcNow
        };

        await _mediaService.AddAsync(media);
        await _mediaService.SaveChangesAsync();

        // Create campaigns for each selected device
        var orderIndex = 0;
        foreach (var deviceId in deviceIds)
        {
            var device = await _deviceService.Query().FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);
            if (device == null) continue;

            // Remove existing campaigns for this device during the same period
            var existingCampaigns = _campaignService.Query()
                .Where(c => c.DeviceId == deviceId && c.IsActive && c.StartDate <= endDateTime && c.EndDate >= startDateTime);
            _campaignService.RemoveRange(existingCampaigns);

            var campaign = new Campaign
            {
                DeviceId = deviceId,
                MediaId = media.Id,
                StartDate = startDateTime,
                EndDate = endDateTime,
                OrderIndex = orderIndex++,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _campaignService.AddAsync(campaign);
        }

        await _campaignService.SaveChangesAsync();

        return Ok(new { 
            message = "Video uploaded successfully",
            fileName = media.FileName,
            fileUrl = media.FileUrl,
            deviceCount = deviceIds.Count 
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
}
