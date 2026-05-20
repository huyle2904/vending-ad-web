using Microsoft.Extensions.Logging;
using VendingAd.Contracts;

namespace VendingAdSystem.Application.Services;

public interface IScheduleCacheEventHandler
{
    Task HandleAsync(ScheduleChangedEvent message, CancellationToken cancellationToken = default);
}

public class ScheduleCacheEventHandler : IScheduleCacheEventHandler
{
    private readonly IMobilePlaybackCacheService _mobilePlaybackCache;
    private readonly ILogger<ScheduleCacheEventHandler> _logger;

    public ScheduleCacheEventHandler(IMobilePlaybackCacheService mobilePlaybackCache, ILogger<ScheduleCacheEventHandler> logger)
    {
        _mobilePlaybackCache = mobilePlaybackCache;
        _logger = logger;
    }

    public async Task HandleAsync(ScheduleChangedEvent message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _mobilePlaybackCache.InvalidateDevicePlaybackCachesAsync(message.AffectedDeviceCodes);

            if (message.ChangeType != ScheduleChangeType.Deleted && message.IsActive == true)
                await _mobilePlaybackCache.WarmScheduleContentAsync(message.ScheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process cache for ScheduleChangedEvent {EventId}.", message.EventId);
        }
    }
}
