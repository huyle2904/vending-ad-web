namespace VendingAdSystem.Application.DTOs;

public class MobileHeartbeatRequest
{
    public string DeviceCode { get; set; } = string.Empty;
}

public class MobileDeviceResponse
{
    public bool Success { get; set; } = true;
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public bool ClaimRequired { get; set; }
    public string? ClaimCode { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? LastSeen { get; set; }
    public MobileAssignedUserResponse? AssignedUser { get; set; }
}

public class MobileAssignedUserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class MobileHeartbeatResponse
{
    public bool Success { get; set; } = true;
    public string DeviceCode { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; }
    public DateTime? LastSeen { get; set; }
}

public class MobilePlaybackStateResponse
{
    public bool Success { get; set; } = true;
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; }
    public bool HasActiveSchedule { get; set; }
    public bool ClaimRequired { get; set; }
    public string? ClaimCode { get; set; }
    public MobileScheduleResponse? Schedule { get; set; }
    public List<MobilePlaybackItemResponse> Items { get; set; } = new();
}

public class MobileScheduleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsImmediate { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public DateTime PlaybackAnchorUtc { get; set; }
}

public class MobilePlaybackItemResponse
{
    public int MediaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public long FileSize { get; set; }
    public string? Checksum { get; set; }
    public int? DurationSeconds { get; set; }
}

public class MobileScheduleContentCache
{
    public MobileScheduleResponse Schedule { get; set; } = new();
    public List<MobilePlaybackItemResponse> Items { get; set; } = new();
}

public class MobileDeviceScheduleCache
{
    public int ScheduleId { get; set; }
    public string Version { get; set; } = string.Empty;
    public bool HasActiveSchedule { get; set; }
    public DateTime ResolvedAtUtc { get; set; }
}
