namespace VendingAdSystem.Domain.Entities;

public class Campaign
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int MediaId { get; set; }
    public Media Media { get; set; } = null!;

    public int OrderIndex { get; set; } = 0;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
