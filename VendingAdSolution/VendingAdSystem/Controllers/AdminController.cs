using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Controllers;

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

    public AdminController(
        ICurrentSession currentSession,
        IUserService userService,
        IDeviceService deviceService,
        ITimeService timeService,
        IMediaService mediaService,
        IRepository<Playlist> playlists,
        IRepository<PlaylistItem> playlistItems,
        IPlaybackScheduleService playbackScheduleService)
    {
        _currentSession = currentSession;
        _userService = userService;
        _deviceService = deviceService;
        _timeService = timeService;
        _mediaService = mediaService;
        _playlists = playlists;
        _playlistItems = playlistItems;
        _playbackScheduleService = playbackScheduleService;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.Query().ToListAsync();
        var userCount = await _userService.Query().CountAsync();
        var deviceCount = devices.Count;
        var onlineCount = devices.Count(d => d.LastSeen.HasValue && (_timeService.UtcNow - d.LastSeen.Value).TotalMinutes < 5);

        ViewBag.UserCount = userCount;
        ViewBag.DeviceCount = deviceCount;
        ViewBag.OnlineCount = onlineCount;
        ViewBag.OfflineCount = deviceCount - onlineCount;

        return View();
    }

    [HttpGet("/admin/devices")]
    public async Task<IActionResult> Devices()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.Query()
            .Include(d => d.User)
            .OrderBy(d => d.DeviceCode)
            .ToListAsync();

        var users = await _userService.Query().Where(u => u.IsActive).OrderBy(u => u.Username).ToListAsync();
        ViewBag.Users = users;

        return View(devices);
    }

    [HttpGet("/admin/videos")]
    public async Task<IActionResult> Videos([FromQuery] int? userId, [FromQuery] string? keyword)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var query = _mediaService.Query()
            .Include(m => m.User)
            .Include(m => m.PlaylistItems)
                .ThenInclude(pi => pi.Playlist)
            .AsQueryable();

        if (userId.HasValue && userId.Value > 0)
            query = query.Where(m => m.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => m.FileName.Contains(keyword.Trim()) || m.PlaylistItems.Any(i => i.Playlist.Name.Contains(keyword.Trim())));

        var videos = await query.OrderByDescending(m => m.UploadedAt).ToListAsync();

        ViewBag.Users = await _userService.Query().OrderBy(u => u.Username).ToListAsync();
        ViewBag.SelectedUserId = userId;
        ViewBag.Keyword = keyword;

        return View("~/Views/Admin/Videos.cshtml", videos);
    }

    [HttpPost("/admin/videos/delete")]
    public async Task<IActionResult> DeleteVideo([FromForm] int videoId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var media = await _mediaService.Query()
            .Include(m => m.PlaylistItems)
            .FirstOrDefaultAsync(m => m.Id == videoId);

        if (media == null)
        {
            TempData["Error"] = "Video not found.";
            return RedirectToAction("Videos");
        }

        foreach (var item in media.PlaylistItems.ToList())
            _playlistItems.Delete(item);

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", media.FileUrl.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _mediaService.Remove(media);
        await _mediaService.SaveChangesAsync();

        TempData["Success"] = "Video deleted successfully.";
        return RedirectToAction("Videos");
    }

    [HttpGet("/admin/playlists")]
    public async Task<IActionResult> Playlists([FromQuery] int? userId, [FromQuery] string? keyword)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var query = _playlists.Query()
            .Include(p => p.User)
            .Include(p => p.Items)
                .ThenInclude(pi => pi.Media)
            .AsQueryable();

        if (userId.HasValue && userId.Value > 0)
            query = query.Where(p => p.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Name.Contains(keyword.Trim()) || p.Items.Any(i => i.Media.FileName.Contains(keyword.Trim())));

        var playlists = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        ViewBag.Devices = await _deviceService.Query().Include(d => d.User).OrderBy(d => d.DeviceCode).ToListAsync();
        ViewBag.Users = await _userService.Query().OrderBy(u => u.Username).ToListAsync();
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

        ViewBag.Users = await _userService.Query().OrderBy(u => u.Username).ToListAsync();
        ViewBag.Devices = await _deviceService.Query().Include(d => d.User).OrderBy(d => d.DeviceCode).ToListAsync();
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
        TempData[updated ? "Success" : "Error"] = updated ? "Schedule updated." : "Schedule not found.";
        return RedirectToAction("Schedules");
    }

    [HttpPost("/admin/schedules/delete")]
    public async Task<IActionResult> DeleteSchedule([FromForm] int scheduleId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var deleted = await _playbackScheduleService.DeleteByIdAsync(scheduleId);
        TempData[deleted ? "Success" : "Error"] = deleted ? "Schedule deleted successfully." : "Delete schedule failed.";
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
            TempData["Error"] = "Playlist not found.";
            return RedirectToAction("Playlists");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Playlist name is required.";
            return RedirectToAction("Playlists");
        }

        playlist.Name = name.Trim();
        playlist.IsActive = isActive;

        await _playlists.SaveChangesAsync();
        TempData["Success"] = "Playlist updated successfully.";
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
            TempData["Error"] = "Playlist not found.";
            return RedirectToAction("Playlists");
        }

        _playlists.Delete(playlist);
        await _playlists.SaveChangesAsync();
        TempData["Success"] = "Playlist deleted successfully.";
        return RedirectToAction("Playlists");
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Users()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var users = await _userService.Query()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

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
            TempData["Error"] = "User not found";
            return RedirectToAction("Users");
        }

        username = username.Trim();
        email = email.Trim();
        fullName = fullName.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Username and email are required";
            return RedirectToAction("Users");
        }

        var exists = await _userService.Query().AnyAsync(u => u.Id != userId && u.Username == username);
        if (exists)
        {
            TempData["Error"] = "Username already exists";
            return RedirectToAction("Users");
        }

        user.Username = username;
        user.Email = email;
        user.FullName = fullName;

        await _userService.SaveChangesAsync();
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
            TempData["Error"] = "User not found";
            return RedirectToAction("Users");
        }

        user.IsActive = !user.IsActive;
        await _userService.SaveChangesAsync();

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
            TempData["Error"] = "Username and email are required";
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
            PasswordHash = HashPassword("TD@12345"),
            IsActive = true,
            CreatedAt = _timeService.UtcNow
        };

        await _userService.AddAsync(user);
        await _userService.SaveChangesAsync();

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
            TempData["Error"] = "User not found";
            return RedirectToAction("Users");
        }

        user.PasswordHash = HashPassword("TD@12345");
        await _userService.SaveChangesAsync();

        TempData["Success"] = $"Password reset for {user.Username}";
        return RedirectToAction("Users");
    }

    [HttpPost("/admin/devices/create")]
    public async Task<IActionResult> CreateDevice([FromForm] string deviceCode, [FromForm] string? location, [FromForm] int userId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            TempData["Error"] = "Device code is required.";
            return RedirectToAction("Devices");
        }

        var existing = await _deviceService.GetByCodeAsync(deviceCode);
        if (existing != null)
        {
            TempData["Error"] = "Device code already exists.";
            return RedirectToAction("Devices");
        }

        var device = new Device
        {
            DeviceCode = deviceCode,
            Location = location,
            UserId = userId,
            IsActive = true,
            LastSeen = null
        };

        await _deviceService.AddAsync(device);
        await _deviceService.SaveChangesAsync();

        TempData["Success"] = $"Device '{deviceCode}' created successfully.";
        return RedirectToAction("Devices");
    }

    [HttpPost("/admin/devices/update")]
    public async Task<IActionResult> UpdateDevice([FromForm] int deviceId, [FromForm] string deviceCode, [FromForm] string? location, [FromForm] int userId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var device = await _deviceService.GetByIdAsync(deviceId);
        if (device == null)
        {
            TempData["Error"] = "Device not found.";
            return RedirectToAction("Devices");
        }

        device.DeviceCode = deviceCode;
        device.Location = location;
        device.UserId = userId;

        await _deviceService.SaveChangesAsync();
        TempData["Success"] = "Device updated successfully.";
        return RedirectToAction("Devices");
    }

    [HttpPost("/admin/devices/delete")]
    public async Task<IActionResult> DeleteDevice([FromForm] int deviceId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var device = await _deviceService.Query()
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
        {
            TempData["Error"] = "Device not found.";
            return RedirectToAction("Devices");
        }

        _deviceService.Remove(device);
        await _deviceService.SaveChangesAsync();

        TempData["Success"] = "Device deleted successfully.";
        return RedirectToAction("Devices");
    }

    private static string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

}
