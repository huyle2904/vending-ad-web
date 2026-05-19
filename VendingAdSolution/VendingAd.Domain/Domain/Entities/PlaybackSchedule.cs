namespace VendingAdSystem.Domain.Entities;

public class PlaybackSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsImmediate { get; set; }
    public DateTime? ImmediateStartedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlaybackScheduleDevice> Devices { get; set; } = new List<PlaybackScheduleDevice>();
    public ICollection<PlaybackScheduleItem> Items { get; set; } = new List<PlaybackScheduleItem>();
}
