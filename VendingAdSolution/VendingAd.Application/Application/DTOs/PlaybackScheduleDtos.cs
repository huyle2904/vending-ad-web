namespace VendingAdSystem.Application.DTOs;

public class PlaybackScheduleRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public int? PlaylistId { get; set; }
    public List<int> DeviceIds { get; set; } = new();
    public List<int> MediaIds { get; set; } = new();
}

public class PlaybackScheduleActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PlaybackScheduleItemOrderUpdate
{
    public int ScheduleItemId { get; set; }
    public int OrderIndex { get; set; }
}

public class PlaybackScheduleItemOrderRequest
{
    public int ScheduleId { get; set; }
    public List<PlaybackScheduleItemOrderUpdate> Updates { get; set; } = new();
}
