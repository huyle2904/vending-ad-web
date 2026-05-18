using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
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
    private readonly IDevicePresenceService _devicePresenceService;

    public PortalController(
        ICurrentSession currentSession,
        IDeviceService deviceService,
        IMediaService mediaService,
        IMediaUploadService mediaUploadService,
        IPlaylistService playlistService,
        ITimeService timeService,
        IPlaylistManagementService playlistManagementService,
        IPlaybackScheduleService playbackScheduleService,
        IDevicePresenceService devicePresenceService)
    {
        _currentSession = currentSession;
        _deviceService = deviceService;
        _mediaService = mediaService;
        _mediaUploadService = mediaUploadService;
        _playlistService = playlistService;
        _timeService = timeService;
        _playlistManagementService = playlistManagementService;
        _playbackScheduleService = playbackScheduleService;
        _devicePresenceService = devicePresenceService;
    }

    private static string DateRangeText(PlaybackSchedule schedule)
    {
        var start = schedule.StartDate.AddHours(7).ToString("dd/MM/yyyy");
        var end = schedule.EndDate.AddHours(7).ToString("dd/MM/yyyy");
        return start == end ? start : $"{start} - {end}";
    }

    private static string TimeRangeText(PlaybackSchedule schedule)
    {
        return $"{schedule.StartTime:hh\\:mm} - {schedule.EndTime:hh\\:mm}";
    }

    private static string LastSeenText(Device device)
    {
        return device.LastSeen.HasValue
            ? device.LastSeen.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm")
            : "Chưa bao giờ";
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
    [AllowAnonymous]
    public IActionResult DashboardHome()
    {
        if (_currentSession.IsAdminLoggedIn || User.IsInRole("Admin"))
            return RedirectToAction("Index", "Admin");

        if (_currentSession.IsPortalLoggedIn || User.IsInRole("User"))
            return RedirectToAction("Dashboard", "Portal");

        return RedirectToAction("Login", "Account");
    }

    [HttpGet("/upload")]
    public IActionResult Upload()
    {
        if (_currentSession.IsAdminLoggedIn || User.IsInRole("Admin"))
            return RedirectToAction("Videos", "Admin");

        if (!_currentSession.IsPortalLoggedIn && !User.IsInRole("User"))
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
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync();


        var schedules = await _playbackScheduleService.GetForUserAsync(userId.Value);
        var now = _timeService.UtcNow;
        var vietnamNow = _timeService.ToVietnamTime(now);
        var currentByDevice = new Dictionary<int, PlaybackSchedule>();
        var upcomingByDevice = new Dictionary<int, PlaybackSchedule>();

        foreach (var device in devices)
        {
            var current = schedules
                .Where(s => s.IsActive)
                .Where(s => s.Devices.Any(d => d.DeviceId == device.Id))
                .Where(s => s.StartDate <= now && s.EndDate >= now)
                .Where(s => IsScheduleActiveNow(s, now))
                .OrderByDescending(s => s.IsImmediate)
                .ThenByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            var upcoming = schedules
                .Where(s => s.IsActive)
                .Where(s => s.Devices.Any(d => d.DeviceId == device.Id))
                .Where(s => current == null || s.Id != current.Id)
                .Select(s => new { Schedule = s, NextStart = GetNextScheduleStartUtc(s, now) })
                .Where(x => x.NextStart.HasValue)
                .OrderBy(x => x.NextStart)
                .ThenBy(x => x.Schedule.StartTime)
                .Select(x => x.Schedule)
                .FirstOrDefault();

            if (current != null)
                currentByDevice[device.Id] = current;
            if (upcoming != null)
                upcomingByDevice[device.Id] = upcoming;
        }

        var currentSchedules = currentByDevice.Values
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .OrderBy(s => s.StartTime)
            .ToList();
        var upcomingSchedules = upcomingByDevice.Values
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .OrderBy(s => GetNextScheduleStartUtc(s, now))
            .ToList();
        var onlineByDeviceCode = await GetOnlineDeviceMapAsync(devices, now);
        var onlineDevices = onlineByDeviceCode.Count(x => x.Value);
        var playlists = await _playlistManagementService.GetPlaylistsForUserAsync(userId.Value);
        var medias = await _mediaService.Query()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

        var nowPlaying = currentSchedules.FirstOrDefault();
        var upcomingScheduleCard = upcomingSchedules.FirstOrDefault();

        var vm = new DashboardViewModel
        {
            DateFilterLabel = $"Hôm nay, {vietnamNow:dd/MM/yyyy}",
            Kpis = new List<KpiViewModel>
            {
                new() { Key = "total", Label = "Tổng thiết bị", Value = devices.Count.ToString(), Description = "2 so với tuần trước", Icon = "bi-pc-display", Tone = "primary" },
                new() { Key = "online", Label = "Trực tuyến", Value = onlineDevices.ToString(), Description = "", Icon = "bi-wifi", Tone = "success" },
                new() { Key = "offline", Label = "Ngoại tuyến", Value = (devices.Count - onlineDevices).ToString(), Description = "", Icon = "bi-power", Tone = "danger" },
                new() { Key = "playing", Label = "Đang phát", Value = currentSchedules.Count.ToString(), Description = "", Icon = "bi-play-circle", Tone = "warning" },
                new() { Key = "upcoming", Label = "Sắp phát", Value = upcomingSchedules.Count.ToString(), Description = "", Icon = "bi-list-task", Tone = "info" },
                new() { Key = "videos", Label = "Tổng video", Value = medias.Count.ToString(), Description = "", Icon = "bi-collection-play", Tone = "primary" }
            },
            NowPlaying = nowPlaying == null ? new PlaylistViewModel { IsEmpty = true } : new PlaylistViewModel
            {
                Name = nowPlaying.Name,
                DateText = DateRangeText(nowPlaying),
                TimeText = TimeRangeText(nowPlaying),
                DeviceCode = nowPlaying.Devices.OrderBy(d => d.Device.DeviceCode).Select(d => d.Device.DeviceCode).FirstOrDefault() ?? "N/A",
                VideoCount = nowPlaying.Items.Count,
                CtaUrl = "/portal/schedules"
            },
            Upcoming = upcomingScheduleCard == null ? new PlaylistViewModel { IsEmpty = true } : new PlaylistViewModel
            {
                Name = upcomingScheduleCard.Name,
                DateText = DateRangeText(upcomingScheduleCard),
                TimeText = TimeRangeText(upcomingScheduleCard),
                DeviceCode = upcomingScheduleCard.Devices.OrderBy(d => d.Device.DeviceCode).Select(d => d.Device.DeviceCode).FirstOrDefault() ?? "N/A",
                VideoCount = upcomingScheduleCard.Items.Count,
                CtaUrl = "/portal/schedules"
            },
            Devices = devices
                .Select(device =>
            {
                var current = currentByDevice.ContainsKey(device.Id) ? currentByDevice[device.Id] : null;
                var next = upcomingByDevice.ContainsKey(device.Id) ? upcomingByDevice[device.Id] : null;
                var isOnline = onlineByDeviceCode.GetValueOrDefault(device.DeviceCode);
                return new DeviceViewModel
                {
                    Id = device.Id,
                    DeviceCode = device.DeviceCode,
                    Location = string.IsNullOrWhiteSpace(device.Location) ? "Chưa có vị trí" : device.Location,
                    IsOnline = isOnline,
                    CurrentPlaylist = current?.Name ?? "Chưa có",
                    UpcomingPlaylist = next?.Name ?? "Chưa có",
                    ContentCount = current?.Items.Count ?? 0,
                    LastActiveText = LastSeenText(device)
                };
            })
            .OrderByDescending(d => d.IsOnline)
            .ThenBy(d => d.DeviceCode)
            .Take(3)
            .ToList()
        };

        return View(vm);
    }

    [HttpGet("/portal/videos")]
    public async Task<IActionResult> Videos([FromQuery] string? q, [FromQuery] string? sortBy, [FromQuery] string? sortDir)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        IQueryable<Media> query = _mediaService.Query()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Include(m => m.PlaylistItems)
            .ThenInclude(pi => pi.Playlist);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(m => m.FileName.Contains(keyword));
        }

        var sortByKey = string.IsNullOrWhiteSpace(sortBy) ? "uploadedAt" : sortBy.Trim().ToLowerInvariant();
        var isAsc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        query = sortByKey switch
        {
            "filename" => isAsc ? query.OrderBy(m => m.FileName) : query.OrderByDescending(m => m.FileName),
            "filesize" => isAsc ? query.OrderBy(m => m.FileSize) : query.OrderByDescending(m => m.FileSize),
            _ => isAsc ? query.OrderBy(m => m.UploadedAt) : query.OrderByDescending(m => m.UploadedAt)
        };

        ViewBag.VideoQuery = q ?? string.Empty;
        ViewBag.VideoSortBy = sortByKey;
        ViewBag.VideoSortDir = isAsc ? "asc" : "desc";

        var videos = await query.ToListAsync();
        return View(videos);
    }

    [HttpGet("/portal/devices")]
    public async Task<IActionResult> Devices([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? sortBy, [FromQuery] string? sortDir)
    {
        if (!IsPortalLoggedIn())
            return RedirectToAction("Login", "Account");

        var userId = _currentSession.UserId ?? 0;

        var devices = await _deviceService.Query()
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync();


        var schedules = await _playbackScheduleService.GetForUserAsync(userId);
        var now = _timeService.UtcNow;
        var vietnamNow = _timeService.ToVietnamTime(now);
        var currentByDevice = new Dictionary<int, PlaybackSchedule>();
        var upcomingByDevice = new Dictionary<int, PlaybackSchedule>();

        foreach (var device in devices)
        {
            var current = schedules
                .Where(s => s.IsActive)
                .Where(s => s.Devices.Any(d => d.DeviceId == device.Id))
                .Where(s => s.StartDate <= now && s.EndDate >= now)
                .Where(s => IsScheduleActiveNow(s, now))
                .OrderByDescending(s => s.IsImmediate)
                .ThenByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            var upcoming = schedules
                .Where(s => s.IsActive)
                .Where(s => s.Devices.Any(d => d.DeviceId == device.Id))
                .Where(s => current == null || s.Id != current.Id)
                .Select(s => new { Schedule = s, NextStart = GetNextScheduleStartUtc(s, now) })
                .Where(x => x.NextStart.HasValue)
                .OrderBy(x => x.NextStart)
                .ThenBy(x => x.Schedule.StartTime)
                .Select(x => x.Schedule)
                .FirstOrDefault();

            if (current != null)
                currentByDevice[device.Id] = current;
            if (upcoming != null)
                upcomingByDevice[device.Id] = upcoming;
        }

        var onlineByDeviceCode = await GetOnlineDeviceMapAsync(devices, now);
        var onlineCount = onlineByDeviceCode.Count(x => x.Value);
        var playlists = await _playlistManagementService.GetPlaylistsForUserAsync(userId);
        var medias = await _mediaService.Query()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

        ViewBag.TotalDevices = devices.Count;
        ViewBag.OnlineCount = onlineCount;
        ViewBag.OfflineCount = devices.Count - onlineCount;
        ViewBag.CurrentScheduleByDevice = currentByDevice;
        ViewBag.UpcomingScheduleByDevice = upcomingByDevice;
        ViewBag.Playlists = playlists;
        ViewBag.Medias = medias;
        ViewBag.UtcNow = now;
        ViewBag.DeviceQuery = q ?? string.Empty;
        ViewBag.DeviceStatus = status ?? string.Empty;
        ViewBag.DeviceSortBy = sortBy ?? "deviceCode";
        ViewBag.DeviceSortDir = sortDir ?? "asc";
        ViewBag.VietnamNow = vietnamNow;
        ViewBag.OnlineByDeviceCode = onlineByDeviceCode;

        return View("~/Views/PortalDevices/Index.cshtml", devices);
    }

    [HttpGet("/portal/device-wall")]
    public async Task<IActionResult> DeviceWall()
    {
        if (!IsPortalLoggedIn())
            return RedirectToAction("Login", "Account");

        var userId = _currentSession.UserId ?? 0;
        var devices = await _deviceService.Query()
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.DeviceCode)
            .ToListAsync();

        ViewBag.TotalDevices = devices.Count;
        var now = _timeService.UtcNow;
        var onlineByDeviceCode = await GetOnlineDeviceMapAsync(devices, now);
        ViewBag.OnlineCount = onlineByDeviceCode.Count(x => x.Value);
        ViewBag.OnlineByDeviceCode = onlineByDeviceCode;

        return View("~/Views/Portal/DeviceWall.cshtml", devices);
    }

    private async Task<Dictionary<string, bool>> GetOnlineDeviceMapAsync(IEnumerable<Device> devices, DateTime utcNow)
    {
        var checks = devices.Select(async device => new
        {
            device.DeviceCode,
            IsOnline = await _devicePresenceService.IsOnlineAsync(device.DeviceCode, device.LastSeen, utcNow)
        });

        var results = await Task.WhenAll(checks);
        return results.ToDictionary(x => x.DeviceCode, x => x.IsOnline);
    }

    [HttpPost("/portal/devices")]
    public async Task<IActionResult> ClaimDevice([FromForm] string claimCode)
    {
        if (!IsPortalLoggedIn())
            return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(claimCode))
        {
            TempData["Error"] = "Mã liên kết là bắt buộc";
            return RedirectToAction("Devices");
        }

        var userId = _currentSession.UserId ?? 0;
        var result = await _deviceService.ClaimAsync(claimCode, userId, _timeService.UtcNow);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
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
            TempData["Error"] = "Không tìm thấy thiết bị";
            return RedirectToAction("Devices");
        }

        _deviceService.Remove(device);
        await _deviceService.SaveChangesAsync();

        TempData["Success"] = "Đã xóa thiết bị";
        return RedirectToAction("Devices");
    }

    [HttpGet("/portal/playlist")]
    public async Task<IActionResult> Playlist()
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        var playlists = await _playlistManagementService.GetPlaylistsForUserAsync(userId.Value);
        ViewBag.PlaylistDraftName = TempData["PlaylistDraftName"] as string ?? string.Empty;
        ViewBag.Medias = await _mediaService.Query()
            .AsNoTracking()
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
        if (!result.Success)
            TempData["PlaylistDraftName"] = name;
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
            TempData["Error"] = "Không tìm thấy danh sách phát";
            return RedirectToAction("Playlist");
        }

        TempData["Success"] = "Đã xóa danh sách phát";
        return RedirectToAction("Playlist");
    }

    [HttpGet("/portal/schedules")]
    public async Task<IActionResult> Schedules()
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return RedirectToAction("Login", "Account");

        ViewBag.Devices = await _deviceService.Query().AsNoTracking().Where(d => d.UserId == userId && d.IsActive).OrderBy(d => d.DeviceCode).ToListAsync();
        ViewBag.Playlists = await _playlistManagementService.GetPlaylistsForUserAsync(userId.Value);
        ViewBag.Medias = await _mediaService.Query().AsNoTracking().Where(m => m.UserId == userId).OrderByDescending(m => m.UploadedAt).ToListAsync();
        ViewBag.ScheduleDraftJson = TempData["ScheduleDraftJson"] as string ?? string.Empty;
        var schedules = await _playbackScheduleService.GetForUserAsync(userId.Value);
        var sortedSchedules = schedules
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.CreatedAt)
            .ToList();
        return View("~/Views/Portal/Schedules.cshtml", sortedSchedules);
    }

    [HttpPost("/portal/schedules/create")]
    public async Task<IActionResult> CreateSchedule([FromForm] PlaybackScheduleRequest request, [FromForm] string actionType, [FromForm] string? sourceTab, [FromForm] int? selectedSourcePlaylistId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = actionType == "immediate"
            ? await _playbackScheduleService.CreateImmediateAsync(userId.Value, request)
            : await _playbackScheduleService.CreateAsync(userId.Value, request);
        if (!result.Success)
        {
            TempData["ScheduleDraftJson"] = JsonSerializer.Serialize(new
            {
                actionType,
                request.Name,
                startDate = request.StartDate.ToString("yyyy-MM-dd"),
                endDate = request.EndDate.ToString("yyyy-MM-dd"),
                startTime = request.StartTime.ToString(@"hh\:mm"),
                endTime = request.EndTime.ToString(@"hh\:mm"),
                deviceIds = request.DeviceIds,
                mediaIds = request.MediaIds,
                sourceTab = string.IsNullOrWhiteSpace(sourceTab) ? "media" : sourceTab,
                selectedSourcePlaylistId,
                request.PlaylistId
            });
        }
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

    [HttpPost("/portal/schedules/add-item")]
    public async Task<IActionResult> AddScheduleItem([FromForm] int scheduleId, [FromForm] int mediaId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var result = await _playbackScheduleService.AddItemAsync(userId.Value, scheduleId, mediaId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Schedules");
    }

    [HttpPost("/portal/schedules/remove-item")]
    public async Task<IActionResult> RemoveScheduleItem([FromForm] int scheduleItemId)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var removed = await _playbackScheduleService.RemoveItemAsync(userId.Value, scheduleItemId);
        TempData[removed ? "Success" : "Error"] = removed ? "Đã xóa video khỏi lịch phát" : "Không tìm thấy mục lịch phát";
        return RedirectToAction("Schedules");
    }

    [HttpPost("/portal/schedules/update-item-order")]
    public async Task<IActionResult> UpdateScheduleItemOrder([FromBody] PlaybackScheduleItemOrderRequest request)
    {
        var userId = _currentSession.UserId;
        if (userId == null || userId <= 0)
            return Unauthorized();

        var updated = await _playbackScheduleService.UpdateItemOrderAsync(userId.Value, request.ScheduleId, request.Updates);
        if (!updated)
            return NotFound(new { success = false, message = "Không tìm thấy lịch phát" });

        return Ok(new { success = true });
    }

    private bool IsScheduleActiveNow(PlaybackSchedule schedule, DateTime utcNow)
    {
        var currentTime = _timeService.ToVietnamTime(utcNow).TimeOfDay;
        if (!schedule.IsImmediate)
            return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;

        if (schedule.ImmediateStartedAt.HasValue && schedule.ImmediateStartedAt.Value.Date == utcNow.Date)
            return currentTime <= schedule.EndTime;

        return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;
    }

    private DateTime GetVietnamDate(DateTime utcDate)
    {
        return _timeService.ToVietnamTime(utcDate).Date;
    }

    private DateTime? GetNextScheduleStartUtc(PlaybackSchedule schedule, DateTime utcNow)
    {
        var vietnamNow = _timeService.ToVietnamTime(utcNow);
        var startDate = GetVietnamDate(schedule.StartDate);
        var endDate = GetVietnamDate(schedule.EndDate);

        if (vietnamNow.Date < startDate)
            return _timeService.ToUtc(startDate.Add(schedule.StartTime));

        if (vietnamNow.Date > endDate)
            return null;

        if (schedule.StartTime > vietnamNow.TimeOfDay)
            return _timeService.ToUtc(vietnamNow.Date.Add(schedule.StartTime));

        var nextDate = vietnamNow.Date.AddDays(1);
        if (nextDate <= endDate)
            return _timeService.ToUtc(nextDate.Add(schedule.StartTime));

        return null;
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
            TempData["Error"] = "Không tìm thấy video trong danh sách phát";
            return RedirectToAction("Playlist");
        }

        TempData["Success"] = "Đã xóa video khỏi danh sách phát";
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
