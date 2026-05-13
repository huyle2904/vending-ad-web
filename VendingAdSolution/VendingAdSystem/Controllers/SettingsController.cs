using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

public class SettingsController : Controller
{
    private readonly ICurrentSession _currentSession;

    public SettingsController(ICurrentSession currentSession)
    {
        _currentSession = currentSession;
    }

    private bool IsPortalLoggedIn()
    {
        return _currentSession.IsPortalLoggedIn;
    }

    [HttpGet("/portal/settings")]
    [HttpGet("/settings")]
    public IActionResult Index()
    {
        if (!IsPortalLoggedIn() && !_currentSession.IsAdminLoggedIn)
            return RedirectToAction("Login", "Account");

        return View();
    }
}
