namespace VendingAdSystem.Domain.Entities;

public class Media
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
}
