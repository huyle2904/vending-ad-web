namespace VendingAdSystem.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public int? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int? TargetId { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }
}
