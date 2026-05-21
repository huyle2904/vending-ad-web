using VendingAdSystem.Application.Services;

namespace VendingAd.Tests;

internal sealed class NullAuditService : IAuditService
{
    public static IAuditService Instance { get; } = new NullAuditService();

    private NullAuditService()
    {
    }

    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class RecordingAuditService : IAuditService
{
    public List<AuditLogEntry> Entries { get; } = new();

    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
