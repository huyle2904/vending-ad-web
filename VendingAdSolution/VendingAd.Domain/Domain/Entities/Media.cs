namespace VendingAdSystem.Domain.Entities;

public enum MediaType
{
    Uploaded = 0,
    YouTube = 1
}

public class Media
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public MediaType MediaType { get; set; } = MediaType.Uploaded;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<PlaylistItem> PlaylistItems { get; set; } = new List<PlaylistItem>();
    public ICollection<PlaybackScheduleItem> PlaybackScheduleItems { get; set; } = new List<PlaybackScheduleItem>();
}
