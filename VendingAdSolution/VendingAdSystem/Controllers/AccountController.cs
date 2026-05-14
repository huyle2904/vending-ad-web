using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AccountController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest request, string? returnUrl)
    {
        if (!ModelState.IsValid)
            return View(request);

        // Try user login first
        var userResponse = await _authService.LoginUserAsync(request);
        if (userResponse.Success)
        {
            // Store token and user info in session
            HttpContext.Session.SetString("AccessToken", userResponse.Token?.AccessToken ?? "");
            HttpContext.Session.SetString("UserEmail", userResponse.User?.Email ?? "");
            HttpContext.Session.SetString("UserRole", "User");
            HttpContext.Session.SetInt32("UserId", userResponse.User?.Id ?? 0);

            var login = !string.IsNullOrWhiteSpace(request.Username) ? request.Username.Trim() : request.Email.Trim();
            var user = await _userService.Query().FirstOrDefaultAsync(u => u.Username == login || u.Email == login);
            if (user != null)
                HttpContext.Session.SetString("UserDisplayName", user.Username);

            return RedirectToLocal(returnUrl);
        }

        // Fallback to admin login
        var adminResponse = await _authService.LoginAdminAsync(request);
        if (adminResponse.Success)
        {
            HttpContext.Session.SetString("AccessToken", adminResponse.Token?.AccessToken ?? "");
            HttpContext.Session.SetString("AdminEmail", adminResponse.User?.Email ?? "");
            HttpContext.Session.SetString("AdminRole", adminResponse.User?.Role ?? "Admin");
            HttpContext.Session.SetInt32("AdminId", adminResponse.User?.Id ?? 0);

            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, userResponse.Message);
        return View(request);
    }

    [HttpGet]
    [ActionName("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId != null && userId > 0)
            return RedirectToAction("Dashboard", "Portal");
        
        return RedirectToAction("Index", "Dashboard");
    }
}
