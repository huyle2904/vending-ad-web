using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace VendingAdSystem.Application.Services;

public class MobileRateLimitOptions
{
    public int WindowSeconds { get; set; } = 60;
    public int DeviceInfoPermitLimit { get; set; } = 20;
    public int HeartbeatPermitLimit { get; set; } = 10;
    public int PlaybackStatePermitLimit { get; set; } = 30;
    public int PlaylistPermitLimit { get; set; } = 30;
}

public enum MobileRateLimitPolicy
{
    DeviceInfo,
    Heartbeat,
    PlaybackState,
    Playlist
}

public record MobileRateLimitResult(bool IsAllowed, int RetryAfterSeconds);

public interface IMobileRateLimitService
{
    MobileRateLimitResult Check(MobileRateLimitPolicy policy, string deviceCode, DateTime utcNow);
}

public class MobileRateLimitService : IMobileRateLimitService
{
    private readonly ConcurrentDictionary<string, RateLimitCounter> _counters = new();
    private readonly MobileRateLimitOptions _options;

    public MobileRateLimitService(IOptions<MobileRateLimitOptions> options)
    {
        _options = options.Value;
    }

    public MobileRateLimitResult Check(MobileRateLimitPolicy policy, string deviceCode, DateTime utcNow)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(deviceCode) ? "unknown" : deviceCode.Trim();
        var window = TimeSpan.FromSeconds(Math.Max(1, _options.WindowSeconds));
        var limit = policy switch
        {
            MobileRateLimitPolicy.DeviceInfo => Math.Max(1, _options.DeviceInfoPermitLimit),
            MobileRateLimitPolicy.Heartbeat => Math.Max(1, _options.HeartbeatPermitLimit),
            MobileRateLimitPolicy.PlaybackState => Math.Max(1, _options.PlaybackStatePermitLimit),
            MobileRateLimitPolicy.Playlist => Math.Max(1, _options.PlaylistPermitLimit),
            _ => Math.Max(1, _options.PlaybackStatePermitLimit)
        };
        var key = $"{policy}:{normalizedCode}";

        CleanupExpiredCounters(utcNow);

        var counter = _counters.GetOrAdd(key, _ => new RateLimitCounter(utcNow, window));

        lock (counter)
        {
            if (utcNow >= counter.WindowEndUtc)
                counter.Reset(utcNow, window);

            if (counter.Count >= limit)
            {
                var retryAfter = Math.Max(1, (int)Math.Ceiling((counter.WindowEndUtc - utcNow).TotalSeconds));
                return new MobileRateLimitResult(false, retryAfter);
            }

            counter.Count++;
            return new MobileRateLimitResult(true, 0);
        }
    }

    private void CleanupExpiredCounters(DateTime utcNow)
    {
        foreach (var item in _counters)
        {
            if (utcNow > item.Value.WindowEndUtc.AddMinutes(1))
                _counters.TryRemove(item.Key, out _);
        }
    }

    private sealed class RateLimitCounter
    {
        public RateLimitCounter(DateTime utcNow, TimeSpan window)
        {
            Reset(utcNow, window);
        }

        public DateTime WindowEndUtc { get; private set; }
        public int Count { get; set; }

        public void Reset(DateTime utcNow, TimeSpan window)
        {
            WindowEndUtc = utcNow.Add(window);
            Count = 0;
        }
    }
}
