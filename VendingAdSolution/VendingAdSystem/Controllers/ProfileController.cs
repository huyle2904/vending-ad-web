using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class ProfileController : Controller
{
    private readonly ICurrentSession _currentSession;
    private readonly IRepository<Admin> _admins;
    private readonly IRepository<User> _users;
    private readonly IPasswordHashingService _passwordHashingService;

    public ProfileController(
        ICurrentSession currentSession,
        IRepository<Admin> admins,
        IRepository<User> users,
        IPasswordHashingService passwordHashingService)
    {
        _currentSession = currentSession;
        _admins = admins;
        _users = users;
        _passwordHashingService = passwordHashingService;
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
    [HttpGet("/profile")]
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

        if (adminId != null)
        {
            var admin = await _admins.GetByIdAsync(adminId.Value);
            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy quản trị viên";
                return RedirectToAction("Index");
            }

            var verification = _passwordHashingService.VerifyPassword(admin.PasswordHash, currentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Current password is incorrect";
                return RedirectToAction("Index");
            }

            admin.PasswordHash = _passwordHashingService.HashPassword(newPassword);
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

            var verification = _passwordHashingService.VerifyPassword(user.PasswordHash, currentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Current password is incorrect";
                return RedirectToAction("Index");
            }

            user.PasswordHash = _passwordHashingService.HashPassword(newPassword);
            await _users.SaveChangesAsync();
            TempData["Success"] = "Đã đổi mật khẩu";
        }

        return RedirectToAction("Index");
    }
}
