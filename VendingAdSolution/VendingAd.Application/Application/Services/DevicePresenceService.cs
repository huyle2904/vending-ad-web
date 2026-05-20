using Microsoft.Extensions.Options;

namespace VendingAdSystem.Application.Services;

public class DevicePresenceOptions
{
    public int OnlineTtlSeconds { get; set; } = 90;
    public int DbWriteIntervalSeconds { get; set; } = 60;
}

public interface IDevicePresenceService
{
    Task MarkOnlineAsync(string deviceCode, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> IsOnlineAsync(string deviceCode, DateTime? lastSeenUtc = null, DateTime? utcNow = null, CancellationToken cancellationToken = default);
    bool ShouldUpdateLastSeen(DateTime? lastSeenUtc, DateTime utcNow);
}

public class DevicePresenceService : IDevicePresenceService
{
    private readonly ICacheService _cacheService;
    private readonly DevicePresenceOptions _options;

    public DevicePresenceService(ICacheService cacheService, IOptions<DevicePresenceOptions> options)
    {
        _cacheService = cacheService;
        _options = options.Value;
    }

    public Task MarkOnlineAsync(string deviceCode, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromSeconds(Math.Max(1, _options.OnlineTtlSeconds));
        return _cacheService.SetAsync(PresenceKey(deviceCode), utcNow, ttl, cancellationToken);
    }

    public async Task<bool> IsOnlineAsync(string deviceCode, DateTime? lastSeenUtc = null, DateTime? utcNow = null, CancellationToken cancellationToken = default)
    {
        if (await _cacheService.ExistsAsync(PresenceKey(deviceCode), cancellationToken))
            return true;

        if (!lastSeenUtc.HasValue)
            return false;

        var now = utcNow ?? DateTime.UtcNow;
        var fallbackWindow = TimeSpan.FromSeconds(Math.Max(1, _options.OnlineTtlSeconds));
        return now - lastSeenUtc.Value <= fallbackWindow;
    }

    public bool ShouldUpdateLastSeen(DateTime? lastSeenUtc, DateTime utcNow)
    {
        if (!lastSeenUtc.HasValue)
            return true;

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.DbWriteIntervalSeconds));
        return utcNow - lastSeenUtc.Value >= interval;
    }

    private static string PresenceKey(string deviceCode)
    {
        return $"device:online:{deviceCode.Trim()}";
    }
}
