using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<FileStorageWriteResult> SaveAsync(
        string sourcePath,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        var normalizedFileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(normalizedFileName))
            throw new ArgumentException("File name must resolve to a valid file.", nameof(fileName));

        var uploadsPath = ResolveUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var destinationPath = Path.Combine(uploadsPath, normalizedFileName);
        try
        {
            await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);

            return new FileStorageWriteResult($"/uploads/{normalizedFileName}");
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }
    }

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryResolveStoredFileName(fileUrl, out var fileName))
        {
            _logger.LogDebug("Skipped local file deletion because URL '{FileUrl}' is not a managed uploads path.", fileUrl);
            return Task.CompletedTask;
        }

        var filePath = Path.Combine(ResolveUploadsPath(), fileName);
        TryDeleteFile(filePath);

        return Task.CompletedTask;
    }

    private string ResolveUploadsPath()
    {
        var configuredPath = _configuration["UploadsPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        var webRootPath = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        return Path.Combine(webRootPath, "uploads");
    }

    private static bool TryResolveStoredFileName(string fileUrl, out string fileName)
    {
        fileName = string.Empty;
        if (string.IsNullOrWhiteSpace(fileUrl))
            return false;

        var path = fileUrl.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            path = absoluteUri.AbsolutePath;

        if (!path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return false;

        fileName = Path.GetFileName(path);
        return !string.IsNullOrWhiteSpace(fileName);
    }

    private static void TryDeleteFile(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
