using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

[AutoValidateAntiforgeryToken]
public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AccountController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpGet("/account/login")]
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl, User.IsInRole("Admin") ? "Admin" : "User");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("/account/login")]
    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(request);

        // Try user login first
        var userResponse = await _authService.LoginUserAsync(request);
        if (userResponse.Success)
        {
            if (userResponse.User == null)
            {
                ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không hợp lệ.");
                return View(request);
            }

            HttpContext.Session.Clear();

            // Store token and user info in session
            HttpContext.Session.SetString("AccessToken", userResponse.Token?.AccessToken ?? "");
            HttpContext.Session.SetString("UserEmail", userResponse.User.Email);
            HttpContext.Session.SetString("UserRole", "User");
            HttpContext.Session.SetInt32("UserId", userResponse.User.Id);

            var login = !string.IsNullOrWhiteSpace(request.Username) ? request.Username.Trim() : request.Email.Trim();
            var user = await _userService.Query().FirstOrDefaultAsync(u => u.Username == login || u.Email == login);
            var displayName = user?.Username ?? userResponse.User.Username;
            HttpContext.Session.SetString("UserDisplayName", displayName);

            await SignInCookieAsync(
                userResponse.User.Id,
                userResponse.User.Email,
                displayName,
                "User",
                "UserId");

            return RedirectToLocal(returnUrl, "User");
        }

        // Fallback to admin login
        var adminResponse = await _authService.LoginAdminAsync(request);
        if (adminResponse.Success)
        {
            if (adminResponse.User == null)
            {
                ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không hợp lệ.");
                return View(request);
            }

            HttpContext.Session.Clear();

            HttpContext.Session.SetString("AccessToken", adminResponse.Token?.AccessToken ?? "");
            HttpContext.Session.SetString("AdminEmail", adminResponse.User.Email);
            HttpContext.Session.SetString("AdminRole", adminResponse.User.Role ?? "Admin");
            HttpContext.Session.SetInt32("AdminId", adminResponse.User.Id);

            await SignInCookieAsync(
                adminResponse.User.Id,
                adminResponse.User.Email,
                string.IsNullOrWhiteSpace(adminResponse.User.Username) ? adminResponse.User.Email : adminResponse.User.Username,
                "Admin",
                "AdminId");

            return RedirectToLocal(returnUrl, "Admin");
        }

        ModelState.AddModelError(string.Empty, userResponse.Message);
        return View(request);
    }

    [Authorize]
    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [Authorize]
    [HttpGet("/account/logout")]
    public IActionResult LogoutGet()
    {
        return RedirectToAction("Login");
    }

    private async Task SignInCookieAsync(int id, string email, string displayName, string role, string idClaimType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new(idClaimType, id.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
            IsPersistent = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    private IActionResult RedirectToLocal(string? returnUrl, string role)
    {
        if (!string.IsNullOrEmpty(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && !IsRootDashboardReturnUrl(returnUrl)
            && IsReturnUrlAllowedForRole(returnUrl, role))
        {
            return Redirect(returnUrl);
        }

        if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Dashboard", "Portal");

        return RedirectToAction("Index", "Admin");
    }

    private static bool IsRootDashboardReturnUrl(string returnUrl)
    {
        var path = returnUrl.Split('?', '#')[0];
        return path.Equals("/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReturnUrlAllowedForRole(string returnUrl, string role)
    {
        var path = returnUrl.Split('?', '#')[0];

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/dashboard", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/profile", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/settings", StringComparison.OrdinalIgnoreCase);

        return !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
    }
}
