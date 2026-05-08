using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Controllers;

public class AdminController : Controller
{
    private readonly ICurrentSession _currentSession;
    private readonly IUserService _userService;
    private readonly IDeviceService _deviceService;
    private readonly ICampaignService _campaignService;

    public AdminController(
        ICurrentSession currentSession,
        IUserService userService,
        IDeviceService deviceService,
        ICampaignService campaignService)
    {
        _currentSession = currentSession;
        _userService = userService;
        _deviceService = deviceService;
        _campaignService = campaignService;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        if (!_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.Query().ToListAsync();
        var userCount = await _userService.Query().CountAsync();
        var deviceCount = devices.Count;
        var onlineCount = devices.Count(d => d.LastSeen.HasValue && (DateTime.UtcNow - d.LastSeen.Value).TotalMinutes < 5);

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
            .Include(d => d.Campaigns)
            .ThenInclude(c => c.Media)
            .OrderBy(d => d.DeviceCode)
            .ToListAsync();

        var users = await _userService.Query().Where(u => u.IsActive).OrderBy(u => u.Username).ToListAsync();
        ViewBag.Users = users;

        return View(devices);
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
            CreatedAt = DateTime.UtcNow
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

    [HttpPost("/admin/users/delete")]
    public async Task<IActionResult> DeleteUser([FromForm] int userId)
    {
        if (!_currentSession.IsAdminLoggedIn)
            return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found";
            return RedirectToAction("Users");
        }

        _userService.Remove(user);
        await _userService.SaveChangesAsync();

        TempData["Success"] = $"User {user.Username} deleted";
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
            .Include(d => d.Campaigns)
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
        {
            TempData["Error"] = "Device not found.";
            return RedirectToAction("Devices");
        }

        _campaignService.RemoveRange(device.Campaigns);
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
