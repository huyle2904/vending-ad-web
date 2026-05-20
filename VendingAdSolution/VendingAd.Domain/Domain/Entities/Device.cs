namespace VendingAdSystem.Domain.Entities;

public class Device
{
    public int Id { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ClaimCode { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public string? DeviceSecretHash { get; set; }
    public DateTime? DeviceSecretCreatedAt { get; set; }
    public DateTime? DeviceSecretRevokedAt { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsActive { get; set; } = true;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<PlaybackScheduleDevice> PlaybackScheduleDevices { get; set; } = new List<PlaybackScheduleDevice>();
}
