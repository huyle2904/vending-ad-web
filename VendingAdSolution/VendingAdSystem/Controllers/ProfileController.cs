using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Controllers;

public class ProfileController : Controller
{
    private readonly ICurrentSession _currentSession;
    private readonly IRepository<Admin> _admins;
    private readonly IRepository<User> _users;

    public ProfileController(ICurrentSession currentSession, IRepository<Admin> admins, IRepository<User> users)
    {
        _currentSession = currentSession;
        _admins = admins;
        _users = users;
    }

    private bool IsLoggedIn()
    {
        return _currentSession.UserId.HasValue || _currentSession.AdminId.HasValue;
    }

    private string? GetUserEmail()
    {
        return _currentSession.UserEmail ?? _currentSession.AdminEmail;
    }

    private string? GetUserRole()
    {
        return _currentSession.AdminId.HasValue ? "Admin" : "User";
    }

    [HttpGet("/account/profile")]
    public IActionResult Index()
    {
        if (!IsLoggedIn())
            return RedirectToAction("Login", "Account");

        ViewBag.Email = GetUserEmail();
        ViewBag.Role = GetUserRole();

        return View();
    }

    [HttpPost("/account/profile/change-password")]
    public async Task<IActionResult> ChangePassword([FromForm] string currentPassword, [FromForm] string newPassword, [FromForm] string confirmPassword)
    {
        if (!IsLoggedIn())
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "Tất cả trường là bắt buộc";
            return RedirectToAction("Index");
        }

        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "New passwords do not match";
            return RedirectToAction("Index");
        }

        if (newPassword.Length < 6)
        {
            TempData["Error"] = "Password must be at least 6 characters";
            return RedirectToAction("Index");
        }

        var adminId = _currentSession.AdminId;
        var userId = _currentSession.UserId;

        var hashedNewPassword = HashPassword(newPassword);

        if (adminId != null)
        {
            var admin = await _admins.GetByIdAsync(adminId.Value);
            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy quản trị viên";
                return RedirectToAction("Index");
            }

            var currentHash = HashPassword(currentPassword);
            if (admin.PasswordHash != currentHash)
            {
                TempData["Error"] = "Current password is incorrect";
                return RedirectToAction("Index");
            }

            admin.PasswordHash = hashedNewPassword;
            await _admins.SaveChangesAsync();
            TempData["Success"] = "Đã đổi mật khẩu";
        }
        else if (userId != null)
        {
            var user = await _users.GetByIdAsync(userId.Value);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng";
                return RedirectToAction("Index");
            }

            var currentHash = HashPassword(currentPassword);
            if (user.PasswordHash != currentHash)
            {
                TempData["Error"] = "Current password is incorrect";
                return RedirectToAction("Index");
            }

            user.PasswordHash = hashedNewPassword;
            await _users.SaveChangesAsync();
            TempData["Success"] = "Đã đổi mật khẩu";
        }

        return RedirectToAction("Index");
    }

    private static string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
