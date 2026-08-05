using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VendingAdSystem.Controllers;

[Route("api/app")]
public sealed class AppUpdateController : Controller
{
    private const long MaxApkBytes = 100 * 1024 * 1024;
    private const string UpdateJsonFileName = "app-update.json";
    private const string ApkFileName = "app-release.apk";
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppUpdateController> _logger;

    public AppUpdateController(
        IConfiguration configuration,
        ILogger<AppUpdateController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("/admin/app-update")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var updateInfo = await ReadUpdateInfoAsync();
        return View("~/Views/Portal/AppUpdate.cshtml", updateInfo ?? new AppUpdateInfo());
    }

    [HttpGet("version")]
    [ResponseCache(NoStore = true, Duration = 0)]
    public async Task<IActionResult> GetVersion()
    {
        var updateInfo = await ReadUpdateInfoAsync();
        return updateInfo == null
            ? NotFound(new { message = "No update available" })
            : Ok(updateInfo);
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxApkBytes)]
    public async Task<IActionResult> UploadApk(
        IFormFile? file,
        [FromForm] string? version,
        [FromForm] string? notes)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File APK là bắt buộc." });

        if (file.Length > MaxApkBytes)
            return BadRequest(new { message = "File APK không được vượt quá 100 MB." });

        if (string.IsNullOrWhiteSpace(version))
            return BadRequest(new { message = "Version là bắt buộc." });

        var normalizedVersion = version.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedVersion, @"^\d+\.\d+\.\d+$"))
            return BadRequest(new { message = "Version phải có định dạng số x.y.z, ví dụ 1.0.1." });

        if (!file.FileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ chấp nhận file .apk." });

        if (notes?.Length > 2_000)
            return BadRequest(new { message = "Ghi chú không được vượt quá 2.000 ký tự." });

        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);
        var temporaryApkPath = Path.Combine(uploadsPath, $".{Guid.NewGuid():N}.apk.tmp");

        try
        {
            await using (var output = new FileStream(
                temporaryApkPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await file.CopyToAsync(output, HttpContext.RequestAborted);
            }

            if (!IsValidApkArchive(temporaryApkPath))
                return BadRequest(new { message = "File không có cấu trúc APK Android hợp lệ." });

            string checksum;
            await using (var input = System.IO.File.OpenRead(temporaryApkPath))
            {
                checksum = Convert.ToHexString(
                    await SHA256.HashDataAsync(input, HttpContext.RequestAborted))
                    .ToLowerInvariant();
            }

            var apkPath = Path.Combine(uploadsPath, ApkFileName);
            System.IO.File.Move(temporaryApkPath, apkPath, overwrite: true);

            var updateInfo = new AppUpdateInfo
            {
                LatestVersion = normalizedVersion,
                ApkUrl = $"/uploads/{ApkFileName}",
                Sha256 = checksum,
                FileSize = file.Length,
                Notes = notes?.Trim() ?? string.Empty,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            };

            var jsonPath = Path.Combine(uploadsPath, UpdateJsonFileName);
            var temporaryJsonPath = $"{jsonPath}.{Guid.NewGuid():N}.tmp";
            await System.IO.File.WriteAllTextAsync(
                temporaryJsonPath,
                JsonSerializer.Serialize(updateInfo, new JsonSerializerOptions { WriteIndented = true }),
                HttpContext.RequestAborted);
            System.IO.File.Move(temporaryJsonPath, jsonPath, overwrite: true);

            _logger.LogInformation(
                "Admin uploaded mobile app version {Version} with SHA-256 {Checksum}",
                normalizedVersion,
                checksum);

            return Ok(new
            {
                success = true,
                message = $"Upload APK version {normalizedVersion} thành công.",
                version = normalizedVersion,
                sha256 = checksum
            });
        }
        finally
        {
            if (System.IO.File.Exists(temporaryApkPath))
                System.IO.File.Delete(temporaryApkPath);
        }
    }

    private string GetUploadsPath()
    {
        return _configuration["UploadsPath"] ?? "uploads";
    }

    private async Task<AppUpdateInfo?> ReadUpdateInfoAsync()
    {
        var jsonPath = Path.Combine(GetUploadsPath(), UpdateJsonFileName);
        if (!System.IO.File.Exists(jsonPath))
            return null;

        try
        {
            await using var stream = System.IO.File.OpenRead(jsonPath);
            var updateInfo = await JsonSerializer.DeserializeAsync<AppUpdateInfo>(stream);
            if (updateInfo == null ||
                string.IsNullOrWhiteSpace(updateInfo.LatestVersion) ||
                string.IsNullOrWhiteSpace(updateInfo.ApkUrl) ||
                !System.Text.RegularExpressions.Regex.IsMatch(
                    updateInfo.Sha256 ?? string.Empty,
                    "^[a-fA-F0-9]{64}$"))
            {
                return null;
            }

            return updateInfo;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "App update metadata is invalid");
            return null;
        }
    }

    private static bool IsValidApkArchive(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            return archive.GetEntry("AndroidManifest.xml") != null &&
                   archive.Entries.Any(entry =>
                       entry.FullName.Equals("classes.dex", StringComparison.OrdinalIgnoreCase) ||
                       entry.FullName.StartsWith("classes", StringComparison.OrdinalIgnoreCase) &&
                       entry.FullName.EndsWith(".dex", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}

public sealed class AppUpdateInfo
{
    public string LatestVersion { get; set; } = string.Empty;
    public string ApkUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}
