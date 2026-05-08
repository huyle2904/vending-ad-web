using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

public class DashboardController : Controller
{
    private readonly IDeviceService _deviceService;

    public DashboardController(IDeviceService deviceService) => _deviceService = deviceService;

    [HttpGet("/")]
    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index()
    {
        var adminEmail = HttpContext.Session.GetString("AdminEmail");
        var userEmail = HttpContext.Session.GetString("UserEmail");
        if (string.IsNullOrEmpty(adminEmail) && string.IsNullOrEmpty(userEmail))
            return RedirectToAction("Login", "Account");

        var devices = await _deviceService.Query()
            .Include(d => d.Campaigns)
            .ThenInclude(c => c.Media)
            .OrderByDescending(d => d.LastSeen)
            .ToListAsync();

        return View(devices);
    }

    [HttpGet("/upload")]
    public IActionResult Upload()
    {
        var adminEmail = HttpContext.Session.GetString("AdminEmail");
        var userEmail = HttpContext.Session.GetString("UserEmail");
        if (string.IsNullOrEmpty(adminEmail) && string.IsNullOrEmpty(userEmail))
            return RedirectToAction("Login", "Account");
        
        return View();
    }
}
