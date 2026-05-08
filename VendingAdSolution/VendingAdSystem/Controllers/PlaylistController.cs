using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api")]
public class PlaylistController : ControllerBase
{
    private readonly ICampaignService _campaignService;

    public PlaylistController(ICampaignService campaignService) => _campaignService = campaignService;

    /// <summary>
    /// GET /api/playlist/{deviceCode}
    /// Returns the assigned video URL for a tablet.
    /// </summary>
    [HttpGet("playlist/{deviceCode}")]
    public async Task<IActionResult> GetPlaylist(string deviceCode)
    {
        var items = await _campaignService.Query()
            .Include(c => c.Device)
            .Include(c => c.Media)
            .Where(c => c.Device.DeviceCode == deviceCode)
            .Select(c => new
            {
                c.Media.FileUrl,
                c.Media.FileName
            })
            .ToListAsync();

        if (!items.Any())
            return NotFound(new { message = $"No campaign assigned to device '{deviceCode}'." });

        return Ok(items);
    }
}
