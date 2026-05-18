using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

public class HomeController : Controller
{
    private readonly ICurrentSession _currentSession;

    public HomeController(ICurrentSession currentSession)
    {
        _currentSession = currentSession;
    }

    public IActionResult Index()
    {
        var userEmail = _currentSession.UserEmail;
        var adminEmail = _currentSession.AdminEmail;

        if (!string.IsNullOrEmpty(adminEmail))
            return RedirectToAction("Index", "Admin");

        if (!string.IsNullOrEmpty(userEmail) && _currentSession.UserId.HasValue)
            return RedirectToAction("Dashboard", "Portal");

        if (!string.IsNullOrEmpty(userEmail))
            return RedirectToAction("Dashboard", "Portal");

        return RedirectToAction("Login", "Account");
    }
}
