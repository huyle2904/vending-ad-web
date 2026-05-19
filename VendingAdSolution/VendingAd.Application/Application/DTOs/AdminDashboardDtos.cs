namespace VendingAdSystem.Application.DTOs;

public class AdminDashboardViewModel
{
    public int UserCount { get; set; }
    public int DeviceCount { get; set; }
    public int OnlineDeviceCount { get; set; }
    public int OfflineDeviceCount { get; set; }
    public int UnassignedDeviceCount { get; set; }
    public int VideoCount { get; set; }
    public long TotalStorageBytes { get; set; }
    public int PlaylistCount { get; set; }
    public int ScheduleCount { get; set; }
    public int ActiveScheduleCount { get; set; }
    public int InactiveScheduleCount { get; set; }
    public int ImmediateScheduleCount { get; set; }
    public int ScheduledScheduleCount { get; set; }
    public int RunningScheduleCount { get; set; }
    public List<DailyUploadStat> UploadsLast7Days { get; set; } = new();
}

public class DailyUploadStat
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
