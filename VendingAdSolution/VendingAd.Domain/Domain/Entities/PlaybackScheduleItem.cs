namespace VendingAdSystem.Domain.Entities;

public class PlaybackScheduleItem
{
    public int Id { get; set; }
    public int PlaybackScheduleId { get; set; }
    public PlaybackSchedule PlaybackSchedule { get; set; } = null!;
    public int MediaId { get; set; }
    public Media Media { get; set; } = null!;
    public int OrderIndex { get; set; }
}
