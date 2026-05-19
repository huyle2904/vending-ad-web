namespace VendingAdSystem.Application.DTOs;

public class DashboardViewModel
{
    public string BreadcrumbSection { get; set; } = "Dashboard";
    public string BreadcrumbPage { get; set; } = "Tổng quan";
    public string Title { get; set; } = "Bảng điều khiển";
    public string Subtitle { get; set; } = "Theo dõi thiết bị, lịch phát và trạng thái hệ thống theo thời gian thực";
    public string DateFilterLabel { get; set; } = string.Empty;
    public List<KpiViewModel> Kpis { get; set; } = new();
    public PlaylistViewModel? NowPlaying { get; set; }
    public PlaylistViewModel? Upcoming { get; set; }
    public List<DeviceViewModel> Devices { get; set; } = new();
}

public class KpiViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "0";
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-circle";
    public string Tone { get; set; } = "primary";
}

public class PlaylistViewModel
{
    public string SectionTitle { get; set; } = string.Empty;
    public string SectionSubtitle { get; set; } = string.Empty;
    public string DotTone { get; set; } = "success";
    public string Name { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public int VideoCount { get; set; }
    public string CtaText { get; set; } = "Xem tất cả";
    public string CtaUrl { get; set; } = "/portal/schedules";
    public string? ThumbnailUrl { get; set; }
    public bool IsEmpty { get; set; }
    public string EmptyMessage { get; set; } = "Chưa có dữ liệu";
}

public class DeviceViewModel
{
    public int Id { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string Location { get; set; } = "Chưa có vị trí";
    public bool IsOnline { get; set; }
    public string CurrentPlaylist { get; set; } = "Chưa có";
    public string UpcomingPlaylist { get; set; } = "Chưa có";
    public int ContentCount { get; set; }
    public string LastActiveText { get; set; } = string.Empty;
}
