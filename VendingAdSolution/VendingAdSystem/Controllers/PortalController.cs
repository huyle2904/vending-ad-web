using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

public class PortalController : Controller
{
    private readonly ICurrentSession _currentSession;
    private readonly IDeviceService _deviceService;
    private readonly IMediaService _mediaService;
    private readonly IMediaUploadService _mediaUploadService;
    private readonly IPlaylistService _playlistService;
    private readonly ITimeService _timeService;
    private readonly IPlaylistManagementService _playlistManagementService;
    private readonly IPlaybackScheduleService _playbackScheduleService;

    public PortalController(
        ICurrentSession currentSession,
        IDeviceService deviceService,
        IMediaService mediaService,
        IMediaUploadService mediaUploadService,
        IPlaylistService playlistService,
        ITimeService timeService,
        IPlaylistManagementService playlistManagementService,
        IPlaybackScheduleService playbackScheduleService)
    {
        _currentSession = currentSession;
        _deviceService = deviceService;
        _mediaService = mediaService;
        _mediaUploadService = mediaUploadService;
        _playlistService = playlistService;
        _timeService = timeService;
        _playlistManagementService = playlistManagementService;
        _playbackScheduleService = playbackScheduleService;
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
            .ToListAsync();

        var totalDevices = devices.Count;
        var onlineDevices = devices.Count(d => d.LastSeen.HasValue && (_timeService.UtcNow - d.LastSeen.Value).TotalMinutes < 5);
        var schedules = await _playbackScheduleService.GetForUserAsync(userId.Value);
        var now = _timeService.UtcNow;
        var activeSchedules = schedules.Where(s => s.IsActive && s.StartDate <= now && s.EndDate >= now).OrderBy(s => s.Name).ToList();

        ViewBag.TotalDevices = totalDevices;
        ViewBag.OnlineDevices = onlineDevices;
        ViewBag.OfflineDevices = totalDevices - onlineDevices;
        ViewBag.TotalSchedules = schedules.Count;
        ViewBag.ActiveSchedules = activeSchedules.Count;
        ViewBag.ActivePlaylists = activeSchedules;

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
            .Include(m => m.PlaylistItems)
            .ThenInclude(pi => pi.Playlist)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

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
            .OrderBy(d => d.DeviceCode)
            .ToListAsync();

        var onlineCount = devices.Count(d => d.LastSeen.HasValue && (_timeService.UtcNow - d.LastSeen.Value).TotalMinutes < 5);

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
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == _currentSession.UserId);

        if (device == null)
        {
            TempData["Error"] = "Device not found";
            return RedirectToAction("Devices");
        }

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

        var playlists = await _playlistManagementService.GetPlaylistsForUserAsync(userId.Value);
        ViewBag.Medias = await _mediaService.Query()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();
        return View(playlists);
    }

    [HttpPost("/portal/playlist/create")]
    public async Task<IActionResult> CreatePlaylist([FromForm] string name)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = await _playlistManagementService.CreateTemplateAsync(name, userId.Value);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Playlist");
    }

    [HttpPost("/portal/videos/delete")]
    public async Task<IActionResult> DeleteVideo([FromForm] int videoId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = await _mediaUploadService.DeleteVideosAsync(new[] { videoId }, userId.Value);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Videos");
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Videos");
    }

    [HttpPost("/portal/videos/batch-delete")]
    public async Task<IActionResult> DeleteVideos([FromForm] List<int> videoIds)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = await _mediaUploadService.DeleteVideosAsync(videoIds, userId.Value);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Videos");
    }

    [HttpPost("/portal/playlist/update-order")]
    public async Task<IActionResult> UpdatePlaylistOrder([FromBody] PlaylistOrderRequest request)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var updated = await _playlistManagementService.UpdatePlaylistOrderAsync(request.PlaylistId, request.Updates, userId.Value);
        if (!updated)
            return NotFound(new { success = false });

        return Ok(new { success = true });
    }

    [HttpPost("/portal/playlist/update")]
    public async Task<IActionResult> UpdatePlaylist([FromForm] UpdatePlaylistRequest request)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = await _playlistManagementService.UpdatePlaylistAsync(request, userId.Value);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Playlist");
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Playlist");
    }

    [HttpPost("/portal/playlist/delete")]
    public async Task<IActionResult> DeletePlaylist([FromForm] int playlistId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var deleted = await _playlistManagementService.DeletePlaylistAsync(playlistId, userId.Value);
        if (!deleted)
        {
            TempData["Error"] = "Playlist not found.";
            return RedirectToAction("Playlist");
        }

        TempData["Success"] = "Playlist deleted successfully.";
        return RedirectToAction("Playlist");
    }

    [HttpGet("/portal/schedules")]
    public async Task<IActionResult> Schedules()
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        ViewBag.Devices = await _deviceService.Query().Where(d => d.UserId == userId && d.IsActive).OrderBy(d => d.DeviceCode).ToListAsync();
        ViewBag.Playlists = await _playlistManagementService.GetPlaylistsForUserAsync(userId.Value);
        ViewBag.Medias = await _mediaService.Query().Where(m => m.UserId == userId).OrderByDescending(m => m.UploadedAt).ToListAsync();
        return View("~/Views/Portal/Schedules.cshtml", await _playbackScheduleService.GetForUserAsync(userId.Value));
    }

    [HttpPost("/portal/schedules/create")]
    public async Task<IActionResult> CreateSchedule([FromForm] PlaybackScheduleRequest request, [FromForm] string actionType)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        if (actionType == "immediate")
        {
            var now = _timeService.ToVietnamTime(_timeService.UtcNow);
            request.StartDate = now.Date;
            request.EndDate = now.Date;
            request.StartTime = now.TimeOfDay;
            request.EndTime = new TimeSpan(23, 59, 0);
        }

        var result = actionType == "immediate"
            ? await _playbackScheduleService.CreateImmediateAsync(userId.Value, request)
            : await _playbackScheduleService.CreateAsync(userId.Value, request);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Schedules");
    }

    [HttpPost("/portal/schedules/toggle")]
    public async Task<IActionResult> ToggleSchedule([FromForm] int scheduleId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();
        await _playbackScheduleService.ToggleAsync(userId.Value, scheduleId);
        return RedirectToAction("Schedules");
    }

    [HttpPost("/portal/schedules/delete")]
    public async Task<IActionResult> DeleteSchedule([FromForm] int scheduleId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();
        await _playbackScheduleService.DeleteAsync(userId.Value, scheduleId);
        return RedirectToAction("Schedules");
    }

    [HttpPost("/portal/playlist/remove-item")]
    public async Task<IActionResult> RemovePlaylistItem([FromForm] int playlistItemId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var removed = await _playlistManagementService.RemovePlaylistItemAsync(playlistItemId, userId.Value);
        if (!removed)
        {
            TempData["Error"] = "Playlist item not found.";
            return RedirectToAction("Playlist");
        }

        TempData["Success"] = "Video removed from playlist.";
        return RedirectToAction("Playlist");
    }

    [HttpPost("/portal/playlist/add-video")]
    public async Task<IActionResult> AddVideoToPlaylist([FromForm] int playlistId, [FromForm] List<int> mediaIds)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = await _playlistManagementService.AddMediasToPlaylistAsync(playlistId, mediaIds, userId.Value);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Playlist");
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Playlist");
    }
}

public class PlaylistOrderRequest
{
    public int PlaylistId { get; set; }
    public List<VendingAdSystem.Application.DTOs.PlaylistOrderUpdate> Updates { get; set; } = new();
}
