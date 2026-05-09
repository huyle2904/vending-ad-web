using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

public class PortalController : Controller
{
    private readonly ICurrentSession _currentSession;
    private readonly IDeviceService _deviceService;
    private readonly IMediaService _mediaService;
    private readonly ICampaignService _campaignService;

    public PortalController(
        ICurrentSession currentSession,
        IDeviceService deviceService,
        IMediaService mediaService,
        ICampaignService campaignService)
    {
        _currentSession = currentSession;
        _deviceService = deviceService;
        _mediaService = mediaService;
        _campaignService = campaignService;
    }

    private bool IsPortalLoggedIn()
    {
        return _currentSession.IsPortalLoggedIn;
    }

    [HttpGet("/portal")]
    public IActionResult Index()
    {
        return RedirectToAction("Dashboard");
    }

    [HttpGet("/")]
    [HttpGet("/dashboard")]
    public async Task<IActionResult> DashboardHome()
    {
        if (!_currentSession.IsAdminLoggedIn && !_currentSession.IsPortalLoggedIn)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.Query()
            .Include(d => d.Campaigns)
            .ThenInclude(c => c.Media)
            .OrderByDescending(d => d.LastSeen)
            .ToListAsync();

        return View("~/Views/Dashboard/Index.cshtml", devices);
    }

    [HttpGet("/upload")]
    public IActionResult Upload()
    {
        if (!_currentSession.IsAdminLoggedIn && !_currentSession.IsPortalLoggedIn)
            return RedirectToAction("Login", "Account");

        return RedirectToAction("Videos");
    }

    [HttpGet("/portal/dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        if (!IsPortalLoggedIn())
            return RedirectToAction("Login", "Account");

        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.Query()
            .Where(d => d.UserId == userId && d.IsActive)
            .Include(d => d.Campaigns)
            .ThenInclude(c => c.Media)
            .ToListAsync();

        var totalDevices = devices.Count;
        var onlineDevices = devices.Count(d => d.LastSeen.HasValue && (DateTime.UtcNow - d.LastSeen.Value).TotalMinutes < 5);
        var totalCampaigns = devices.SelectMany(d => d.Campaigns).Count(c => c.IsActive);
        var activeCampaigns = devices.SelectMany(d => d.Campaigns).Count(c => c.IsActive && c.StartDate <= DateTime.UtcNow && c.EndDate >= DateTime.UtcNow);

        var now = DateTime.UtcNow;
        var activeCampaignsList = await _campaignService.Query()
            .Where(c => c.Device.UserId == userId && c.IsActive && c.StartDate <= now && c.EndDate >= now)
            .Include(c => c.Device)
            .Include(c => c.Media)
            .OrderBy(c => c.Device.DeviceCode)
            .ThenBy(c => c.OrderIndex)
            .ToListAsync();

        ViewBag.TotalDevices = totalDevices;
        ViewBag.OnlineDevices = onlineDevices;
        ViewBag.OfflineDevices = totalDevices - onlineDevices;
        ViewBag.TotalCampaigns = totalCampaigns;
        ViewBag.ActiveCampaigns = activeCampaigns;
        ViewBag.ActiveCampaignsList = activeCampaignsList;

        return View(devices);
    }

    [HttpGet("/portal/videos")]
    public async Task<IActionResult> Videos()
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        var videos = await _mediaService.Query()
            .Where(m => m.UserId == userId)
            .Include(m => m.Campaigns)
            .ThenInclude(c => c.Device)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

        var devices = await _deviceService.Query()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.DeviceCode)
            .ToListAsync();

        ViewBag.Devices = devices;

        return View(videos);
    }

    [HttpGet("/portal/devices")]
    public async Task<IActionResult> Devices()
    {
        if (!IsPortalLoggedIn())
            return RedirectToAction("Login", "Account");

        var userId = _currentSession.UserId ?? 0;

        var devices = await _deviceService.Query()
            .Where(d => d.UserId == userId && d.IsActive)
            .Include(d => d.Campaigns)
            .ThenInclude(c => c.Media)
            .OrderBy(d => d.DeviceCode)
            .ToListAsync();

        var onlineCount = devices.Count(d => d.LastSeen.HasValue && (DateTime.UtcNow - d.LastSeen.Value).TotalMinutes < 5);

        ViewBag.TotalDevices = devices.Count;
        ViewBag.OnlineCount = onlineCount;
        ViewBag.OfflineCount = devices.Count - onlineCount;

        return View("~/Views/PortalDevices/Index.cshtml", devices);
    }

    [HttpPost("/portal/devices")]
    public async Task<IActionResult> CreateDevice([FromForm] string deviceCode, [FromForm] string location)
    {
        if (!IsPortalLoggedIn())
            return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            TempData["Error"] = "Device code is required";
            return RedirectToAction("Devices");
        }

        var existing = await _deviceService.GetByCodeAsync(deviceCode);
        if (existing != null)
        {
            TempData["Error"] = "Device code already exists";
            return RedirectToAction("Devices");
        }

        var device = new Device
        {
            DeviceCode = deviceCode,
            Location = location,
            UserId = _currentSession.UserId ?? 0,
            IsActive = true,
            LastSeen = null
        };

        await _deviceService.AddAsync(device);
        await _deviceService.SaveChangesAsync();

        TempData["Success"] = "Device added successfully";
        return RedirectToAction("Devices");
    }

    [HttpPost("/portal/devices/delete")]
    public async Task<IActionResult> DeleteDevice([FromForm] int deviceId)
    {
        if (!IsPortalLoggedIn())
            return Unauthorized();

        var device = await _deviceService.Query()
            .Include(d => d.Campaigns)
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == _currentSession.UserId);

        if (device == null)
        {
            TempData["Error"] = "Device not found";
            return RedirectToAction("Devices");
        }

        _campaignService.RemoveRange(device.Campaigns);
        _deviceService.Remove(device);
        await _deviceService.SaveChangesAsync();

        TempData["Success"] = "Device deleted successfully";
        return RedirectToAction("Devices");
    }

    [HttpGet("/portal/playlist")]
    public async Task<IActionResult> Playlist()
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        var now = DateTime.UtcNow;
        var campaigns = await _campaignService.Query()
            .Where(c => c.Device.UserId == userId && c.IsActive && c.StartDate <= now && c.EndDate >= now)
            .Include(c => c.Device)
            .Include(c => c.Media)
            .OrderBy(c => c.Device.DeviceCode)
            .ThenBy(c => c.OrderIndex)
            .ToListAsync();

        // Group by device - use concrete type
        var grouped = campaigns
            .GroupBy(c => c.Device)
            .Select(g => new DevicePlaylistGroup
            {
                Device = g.Key,
                Videos = g.OrderBy(c => c.OrderIndex).ToList()
            })
            .OrderBy(g => g.Device.DeviceCode)
            .ToList();

        ViewBag.GroupedCampaigns = grouped;

        return View(campaigns);
    }

    [HttpPost("/portal/videos/delete")]
    public async Task<IActionResult> DeleteVideo([FromForm] int videoId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var video = await _mediaService.Query()
            .Include(m => m.Campaigns)
            .FirstOrDefaultAsync(m => m.Id == videoId && m.UserId == userId);

        if (video == null)
        {
            TempData["Error"] = "Video not found.";
            return RedirectToAction("Videos");
        }

        // Delete campaigns first
        _campaignService.RemoveRange(video.Campaigns);
        
        // Delete file
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", video.FileUrl.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _mediaService.Remove(video);
        await _mediaService.SaveChangesAsync();

        TempData["Success"] = "Video deleted successfully.";
        return RedirectToAction("Videos");
    }

    [HttpPost("/portal/playlist/update-order")]
    public async Task<IActionResult> UpdatePlaylistOrder([FromBody] List<PlaylistUpdate> updates)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        foreach (var update in updates)
        {
            var campaign = await _campaignService.Query()
                .FirstOrDefaultAsync(c => c.Id == update.CampaignId && c.Device.UserId == userId);
            
            if (campaign != null)
            {
                campaign.OrderIndex = update.OrderIndex;
            }
        }

        await _campaignService.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

public class PlaylistUpdate
{
    public int CampaignId { get; set; }
    public int OrderIndex { get; set; }
}

public class DevicePlaylistGroup
{
    public Device Device { get; set; } = null!;
    public List<Campaign> Videos { get; set; } = new();
}
