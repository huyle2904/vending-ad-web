using Microsoft.AspNetCore.Http;

namespace VendingAdSystem.Application.DTOs;

public class UploadVideoRequest
{
    public IFormFile? File { get; set; }
    public int UserId { get; set; }
}

public class UploadVideoResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public int DeviceCount { get; set; }
    public int? PlaylistId { get; set; }
    public string? PlaylistName { get; set; }
}

public class PlaylistResponse
{
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

public class HeartbeatRequestDto
{
    public string DeviceCode { get; set; } = string.Empty;
}

public class RegisterDeviceRequestDto
{
    public string DeviceCode { get; set; } = string.Empty;
    public string? Location { get; set; }
}
