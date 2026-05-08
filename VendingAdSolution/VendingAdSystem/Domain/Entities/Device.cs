namespace VendingAdSystem.Domain.Entities;

public class Device
{
    public int Id { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsActive { get; set; } = true;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
}
