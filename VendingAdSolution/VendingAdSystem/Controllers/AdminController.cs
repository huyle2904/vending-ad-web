using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Controllers;

[Authorize(Roles = "Admin")]
[AutoValidateAntiforgeryToken]
public class AdminController : Controller
{
    private readonly ICurrentSession _currentSession;
    private readonly IUserService _userService;
    private readonly IDeviceService _deviceService;
    private readonly ITimeService _timeService;
    private readonly IMediaService _mediaService;
    private readonly IRepository<Playlist> _playlists;
    private readonly IRepository<PlaylistItem> _playlistItems;
    private readonly IPlaybackScheduleService _playbackScheduleService;
    private readonly IDevicePresenceService _devicePresenceService;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IDeviceCredentialService _deviceCredentialService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditService _auditService;

    public AdminController(
        ICurrentSession currentSession,
        IUserService userService,
        IDeviceService deviceService,
        ITimeService timeService,
        IMediaService mediaService,
        IRepository<Playlist> playlists,
        IRepository<PlaylistItem> playlistItems,
        IPlaybackScheduleService playbackScheduleService,
        IDevicePresenceService devicePresenceService,
        IPasswordHashingService passwordHashingService,
        IDeviceCredentialService deviceCredentialService,
        IFileStorageService fileStorageService,
        IAuditService auditService)
    {
        _currentSession = currentSession;
        _userService = userService;
        _deviceService = deviceService;
        _timeService = timeService;
        _mediaService = mediaService;
        _playlists = playlists;
        _playlistItems = playlistItems;
        _playbackScheduleService = playbackScheduleService;
        _devicePresenceService = devicePresenceService;
        _passwordHashingService = passwordHashingService;
        _deviceCredentialService = deviceCredentialService;
        _fileStorageService = fileStorageService;
        _auditService = auditService;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.GetAllDevicesAsync();
        var medias = await _mediaService.GetAllMediaAsync();
        var playlists = await _playlists.Query().AsNoTracking().ToListAsync();
        var schedules = await _playbackScheduleService.GetAllAsync();
        var now = _timeService.UtcNow;
        var vietnamToday = _timeService.ToVietnamTime(now).Date;
        var onlineByDeviceCode = await GetOnlineDeviceMapAsync(devices, now);
        var onlineCount = onlineByDeviceCode.Count(x => x.Value);

        var model = new AdminDashboardViewModel
        {
            UserCount = await _userService.GetTotalUserCountAsync(),
            DeviceCount = devices.Count,
            OnlineDeviceCount = onlineCount,
            OfflineDeviceCount = devices.Count - onlineCount,
            UnassignedDeviceCount = devices.Count(d => d.UserId == null),
            VideoCount = medias.Count,
            TotalStorageBytes = medias.Sum(m => m.FileSize),
            PlaylistCount = playlists.Count,
            ScheduleCount = schedules.Count(),
            ActiveScheduleCount = schedules.Count(s => s.IsActive),
            InactiveScheduleCount = schedules.Count(s => !s.IsActive),
            ImmediateScheduleCount = schedules.Count(s => s.IsImmediate),
            ScheduledScheduleCount = schedules.Count(s => !s.IsImmediate),
            RunningScheduleCount = schedules.Count(s => IsScheduleRunningNow(s, now))
        };

        model.UploadsLast7Days = Enumerable.Range(0, 7)
            .Select(offset => vietnamToday.AddDays(offset - 6))
            .Select(date => new DailyUploadStat
            {
                Label = date.ToString("dd/MM"),
                Count = medias.Count(m => _timeService.ToVietnamTime(m.UploadedAt).Date == date)
            })
            .ToList();

        return View(model);
    }

    [HttpGet("/admin/devices")]
    public async Task<IActionResult> Devices()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.GetAllDevicesWithUsersAsync();
        var users = await _userService.GetAllActiveUsersAsync();
        ViewBag.Users = users;
        ViewBag.OnlineByDeviceCode = await GetOnlineDeviceMapAsync(devices, _timeService.UtcNow);

        return View(devices);
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

    [HttpGet("/admin/videos")]
    public async Task<IActionResult> Videos([FromQuery] int? userId, [FromQuery] string? keyword)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var videos = await _mediaService.GetAllMediaWithDetailsAsync(userId, keyword);

        ViewBag.Users = await _userService.GetAllUsersSortedAsync();
        ViewBag.SelectedUserId = userId;
        ViewBag.Keyword = keyword;

        return View("~/Views/Admin/Videos.cshtml", videos);
    }

    [HttpPost("/admin/videos/delete")]
    public async Task<IActionResult> DeleteVideo([FromForm] int videoId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var media = await _mediaService.GetMediaWithPlaylistItemsAsync(videoId);

        if (media == null)
        {
            TempData["Error"] = "Không tìm thấy video.";
            return RedirectToAction("Videos");
        }

        foreach (var item in media.PlaylistItems.ToList())
            _playlistItems.Delete(item);

        await _fileStorageService.DeleteAsync(media.FileUrl);
        _mediaService.Remove(media);
        await _mediaService.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.DeleteVideo,
            TargetType = AuditTargets.Media,
            TargetId = media.Id,
            Details = new
            {
                media.FileName,
                media.UserId
            }
        });

        TempData["Success"] = "Đã xóa video.";
        return RedirectToAction("Videos");
    }

    [HttpGet("/admin/playlists")]
    public async Task<IActionResult> Playlists([FromQuery] int? userId, [FromQuery] string? keyword)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var query = _playlists.Query()
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Items)
                .ThenInclude(pi => pi.Media)
            .AsQueryable();

        if (userId.HasValue && userId.Value > 0)
            query = query.Where(p => p.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Name.Contains(keyword.Trim()) || p.Items.Any(i => i.Media.FileName.Contains(keyword.Trim())));

        var playlists = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        ViewBag.Devices = await _deviceService.GetAllDevicesWithUsersAsync();
        ViewBag.Users = await _userService.GetAllUsersSortedAsync();
        ViewBag.SelectedUserId = userId;
        ViewBag.Keyword = keyword;
        return View("~/Views/Admin/Playlists.cshtml", playlists);
    }

    [HttpGet("/admin/schedules")]
    public async Task<IActionResult> Schedules([FromQuery] int? userId, [FromQuery] int? deviceId, [FromQuery] string? keyword)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var allSchedules = await _playbackScheduleService.GetAllAsync();
        var query = allSchedules.AsQueryable();

        if (userId.HasValue && userId.Value > 0)
            query = query.Where(s => s.UserId == userId.Value);

        if (deviceId.HasValue && deviceId.Value > 0)
            query = query.Where(s => s.Devices.Any(d => d.DeviceId == deviceId.Value));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(s => s.Name.Contains(key) || s.Items.Any(i => i.Media.FileName.Contains(key)) || s.User.Username.Contains(key));
        }

        var filteredSchedules = query
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        ViewBag.Users = await _userService.GetAllUsersSortedAsync();
        ViewBag.Devices = await _deviceService.GetAllDevicesWithUsersAsync();
        ViewBag.SelectedUserId = userId;
        ViewBag.SelectedDeviceId = deviceId;
        ViewBag.Keyword = keyword;
        return View("~/Views/Admin/Schedules.cshtml", filteredSchedules);
    }

    [HttpPost("/admin/schedules/toggle")]
    public async Task<IActionResult> ToggleSchedule([FromForm] int scheduleId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var updated = await _playbackScheduleService.ToggleByIdAsync(scheduleId);
        TempData[updated ? "Success" : "Error"] = updated ? "Đã cập nhật lịch phát." : "Không tìm thấy lịch phát.";
        return RedirectToAction("Schedules");
    }

    [HttpPost("/admin/schedules/delete")]
    public async Task<IActionResult> DeleteSchedule([FromForm] int scheduleId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var deleted = await _playbackScheduleService.DeleteByIdAsync(scheduleId);
        TempData[deleted ? "Success" : "Error"] = deleted ? "Đã xóa lịch phát." : "Xóa lịch phát thất bại.";
        return RedirectToAction("Schedules");
    }

    [HttpPost("/admin/playlists/update")]
    public async Task<IActionResult> UpdatePlaylist([FromForm] int playlistId, [FromForm] string name, [FromForm] bool isActive)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var playlist = await _playlists.Query()
            .FirstOrDefaultAsync(p => p.Id == playlistId);

        if (playlist == null)
        {
            TempData["Error"] = "Không tìm thấy danh sách phát.";
            return RedirectToAction("Playlists");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên danh sách phát là bắt buộc.";
            return RedirectToAction("Playlists");
        }

        playlist.Name = name.Trim();
        playlist.IsActive = isActive;

        await _playlists.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật danh sách phát.";
        return RedirectToAction("Playlists");
    }

    [HttpPost("/admin/playlists/delete")]
    public async Task<IActionResult> DeletePlaylist([FromForm] int playlistId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var playlist = await _playlists.Query().FirstOrDefaultAsync(p => p.Id == playlistId);
        if (playlist == null)
        {
            TempData["Error"] = "Không tìm thấy danh sách phát.";
            return RedirectToAction("Playlists");
        }

        _playlists.Delete(playlist);
        await _playlists.SaveChangesAsync();
        TempData["Success"] = "Đã xóa danh sách phát.";
        return RedirectToAction("Playlists");
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Users()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var users = await _userService.GetAllUsersAsync();

        return View("~/Views/AdminUsers/Index.cshtml", users);
    }

    [HttpPost("/admin/users/update")]
    public async Task<IActionResult> UpdateUser([FromForm] int userId, [FromForm] string username, [FromForm] string email, [FromForm] string fullName)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy người dùng";
            return RedirectToAction("Users");
        }

        username = username.Trim();
        email = email.Trim();
        fullName = fullName.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Tên đăng nhập và email là bắt buộc";
            return RedirectToAction("Users");
        }

        var exists = await _userService.IsUsernameTakenAsync(username, userId);
        if (exists)
        {
            TempData["Error"] = "Username already exists";
            return RedirectToAction("Users");
        }

        user.Username = username;
        user.Email = email;
        user.FullName = fullName;

        await _userService.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.UpdateUser,
            TargetType = AuditTargets.User,
            TargetId = user.Id,
            Details = new
            {
                user.Username,
                user.Email,
                user.FullName
            }
        });
        TempData["Success"] = $"User {username} updated";
        return RedirectToAction("Users");
    }

    [HttpPost("/admin/users/toggle-active")]
    public async Task<IActionResult> ToggleUserActive([FromForm] int userId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy người dùng";
            return RedirectToAction("Users");
        }

        user.IsActive = !user.IsActive;
        await _userService.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.ToggleUserActive,
            TargetType = AuditTargets.User,
            TargetId = user.Id,
            Details = new
            {
                user.Username,
                user.IsActive
            }
        });

        TempData["Success"] = user.IsActive ? $"User {user.Username} activated" : $"User {user.Username} disabled";
        return RedirectToAction("Users");
    }

    [HttpPost("/admin/users/create")]
    public async Task<IActionResult> CreateUser([FromForm] string username, [FromForm] string email, [FromForm] string fullName)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        username = username.Trim();
        email = email.Trim();
        fullName = fullName.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Tên đăng nhập và email là bắt buộc";
            return RedirectToAction("Users");
        }

        var exists = await _userService.GetByUsernameAsync(username);
        if (exists != null)
        {
            TempData["Error"] = "Username already exists";
            return RedirectToAction("Users");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            PasswordHash = _passwordHashingService.HashPassword("TD@12345"),
            IsActive = true,
            CreatedAt = _timeService.UtcNow
        };

        await _userService.AddAsync(user);
        await _userService.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.CreateUser,
            TargetType = AuditTargets.User,
            TargetId = user.Id,
            Details = new
            {
                user.Username,
                user.Email,
                user.FullName
            }
        });

        TempData["Success"] = $"User {username} created with default password TD@12345";
        return RedirectToAction("Users");
    }

    [HttpPost("/admin/users/reset-password")]
    public async Task<IActionResult> ResetUserPassword([FromForm] int userId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy người dùng";
            return RedirectToAction("Users");
        }

        user.PasswordHash = _passwordHashingService.HashPassword("TD@12345");
        await _userService.SaveChangesAsync();
        await _auditService.LogAsync(new AuditLogEntry
        {
            Action = AuditActions.ResetPassword,
            TargetType = AuditTargets.User,
            TargetId = user.Id,
            Details = new
            {
                user.Username
            }
        });

        TempData["Success"] = $"Password reset for {user.Username}";
        return RedirectToAction("Users");
    }

    [HttpPost("/admin/devices/assign")]
    public async Task<IActionResult> AssignDevice([FromForm] int deviceId, [FromForm] int userId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            TempData["Error"] = "Tài khoản gán không hợp lệ.";
            return RedirectToAction("Devices");
        }

        var device = await _deviceService.GetByIdAsync(deviceId);
        if (device == null)
        {
            TempData["Error"] = "Không tìm thấy thiết bị.";
            return RedirectToAction("Devices");
        }

        if (device.UserId.HasValue)
        {
            TempData["Error"] = "Thiết bị đã được gán cho tài khoản.";
            return RedirectToAction("Devices");
        }

        device.UserId = userId;
        device.ClaimCode = null;
        device.ClaimedAt = _timeService.UtcNow;

        await _deviceService.SaveChangesAsync();
        TempData["Success"] = $"Đã gán thiết bị '{device.DeviceCode}' cho {user.Username}.";
        return RedirectToAction("Devices");
    }

    [HttpPost("/admin/devices/delete")]
    public async Task<IActionResult> DeleteDevice([FromForm] int deviceId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var device = await _deviceService.GetByIdAsync(deviceId);

        if (device == null)
        {
            TempData["Error"] = "Không tìm thấy thiết bị.";
            return RedirectToAction("Devices");
        }

        _deviceService.Remove(device);
        await _deviceService.SaveChangesAsync();

        TempData["Success"] = "Đã xóa thiết bị.";
        return RedirectToAction("Devices");
    }

    [HttpPost("/admin/devices/rotate-secret")]
    public async Task<IActionResult> RotateDeviceSecret([FromForm] int deviceId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var result = await _deviceCredentialService.RotateSecretAsync(deviceId, _timeService.UtcNow);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Devices");
        }

        TempData["Success"] = $"Đã cấp lại secret cho thiết bị {result.DeviceCode}. Secret chỉ hiển thị một lần.";
        TempData["DeviceSecretDeviceCode"] = result.DeviceCode;
        TempData["DeviceSecret"] = result.DeviceSecret;
        return RedirectToAction("Devices");
    }

    [HttpPost("/admin/devices/revoke-secret")]
    public async Task<IActionResult> RevokeDeviceSecret([FromForm] int deviceId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var result = await _deviceCredentialService.RevokeSecretAsync(deviceId, _timeService.UtcNow);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? $"Đã thu hồi secret của thiết bị {result.DeviceCode}."
            : result.Message;

        return RedirectToAction("Devices");
    }

    private bool IsScheduleRunningNow(PlaybackSchedule schedule, DateTime utcNow)
    {
        if (!schedule.IsActive || schedule.StartDate > utcNow || schedule.EndDate < utcNow)
            return false;

        var currentTime = _timeService.ToVietnamTime(utcNow).TimeOfDay;
        return currentTime >= schedule.StartTime && currentTime <= schedule.EndTime;
    }

}
