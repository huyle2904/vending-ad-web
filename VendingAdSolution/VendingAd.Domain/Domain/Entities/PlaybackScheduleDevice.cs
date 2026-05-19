namespace VendingAdSystem.Domain.Entities;

public class PlaybackScheduleDevice
{
    public int Id { get; set; }
    public int PlaybackScheduleId { get; set; }
    public PlaybackSchedule PlaybackSchedule { get; set; } = null!;
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
}
