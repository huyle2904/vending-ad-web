namespace VendingAd.Contracts;

public enum ScheduleChangeType
{
    Created,
    Updated,
    Deleted,
    Toggled,
    ItemAdded,
    ItemRemoved,
    Reordered
}

public class ScheduleChangedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; }
    public int ScheduleId { get; set; }
    public int? UserId { get; set; }
    public ScheduleChangeType ChangeType { get; set; }
    public bool? IsActive { get; set; }
    public List<string> AffectedDeviceCodes { get; set; } = new();
}

public class VideoUploadedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; }
    public int MediaId { get; set; }
    public int? UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
}
