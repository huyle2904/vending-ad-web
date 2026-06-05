using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VendingAdSystem.Controllers;

[ApiController]
[Route("api/app")]
public class AppUpdateController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private const string UpdateJsonFileName = "app-update.json";
    private const string ApkFileName = "app-release.apk";

    public AppUpdateController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("version")]
    [ResponseCache(NoStore = true, Duration = 0)]
    public IActionResult GetVersion()
    {
        var uploadsPath = _configuration["UploadsPath"] ?? "uploads";
        var jsonPath = Path.Combine(uploadsPath, UpdateJsonFileName);

        if (!System.IO.File.Exists(jsonPath))
            return NotFound(new { message = "No update available" });

        var json = System.IO.File.ReadAllText(jsonPath);
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        return Ok(data ?? new Dictionary<string, object?>());
    }

    [HttpPost("upload")]
    [Authorize(Roles = "User")]
    [RequestSizeLimit(104_857_600)] // 100 MB
    public async Task<IActionResult> UploadApk(IFormFile? file, [FromForm] string version, [FromForm] string? notes)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File APK là bắt buộc." });

        if (string.IsNullOrWhiteSpace(version))
            return BadRequest(new { message = "Version là bắt buộc." });

        if (!file.FileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ chấp nhận file .apk." });

        var uploadsPath = _configuration["UploadsPath"] ?? "uploads";
        Directory.CreateDirectory(uploadsPath);

        // Save APK file
        var apkPath = Path.Combine(uploadsPath, ApkFileName);
        await using (var stream = new FileStream(apkPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Write update metadata JSON
        var updateInfo = new Dictionary<string, object?>
        {
            ["latestVersion"] = version.Trim(),
            ["apkUrl"] = $"/uploads/{ApkFileName}",
            ["notes"] = notes?.Trim() ?? "",
            ["updatedAt"] = DateTime.UtcNow.ToString("o")
        };

        var jsonPath = Path.Combine(uploadsPath, UpdateJsonFileName);
        await System.IO.File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(updateInfo, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return Ok(new
        {
            success = true,
            message = $"Upload APK version {version} thành công.",
            version = version.Trim()
        });
    }
}
