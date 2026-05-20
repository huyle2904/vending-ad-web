using Microsoft.Extensions.Logging.Abstractions;
using VendingAd.Contracts;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;
using Xunit;

namespace VendingAd.Tests;

public class ScheduleCacheEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_InvalidatesAllAffectedDeviceCodes()
    {
        var cache = new RecordingMobilePlaybackCacheService();
        var handler = new ScheduleCacheEventHandler(cache, NullLogger<ScheduleCacheEventHandler>.Instance);

        await handler.HandleAsync(new ScheduleChangedEvent
        {
            ScheduleId = 10,
            ChangeType = ScheduleChangeType.Updated,
            IsActive = false,
            AffectedDeviceCodes = new List<string> { "OLD-1", "NEW-1" }
        });

        Assert.Equal(new[] { "OLD-1", "NEW-1" }, cache.InvalidatedDeviceCodes);
    }

    [Fact]
    public async Task HandleAsync_WarmsActiveScheduleContent()
    {
        var cache = new RecordingMobilePlaybackCacheService();
        var handler = new ScheduleCacheEventHandler(cache, NullLogger<ScheduleCacheEventHandler>.Instance);

        await handler.HandleAsync(new ScheduleChangedEvent
        {
            ScheduleId = 10,
            ChangeType = ScheduleChangeType.Updated,
            IsActive = true,
            AffectedDeviceCodes = new List<string> { "DEVICE-1" }
        });

        Assert.Equal(new[] { 10 }, cache.WarmedScheduleIds);
    }

    [Theory]
    [InlineData(ScheduleChangeType.Deleted, true)]
    [InlineData(ScheduleChangeType.Updated, false)]
    public async Task HandleAsync_SkipsWarmForDeletedOrInactiveSchedules(ScheduleChangeType changeType, bool isActive)
    {
        var cache = new RecordingMobilePlaybackCacheService();
        var handler = new ScheduleCacheEventHandler(cache, NullLogger<ScheduleCacheEventHandler>.Instance);

        await handler.HandleAsync(new ScheduleChangedEvent
        {
            ScheduleId = 10,
            ChangeType = changeType,
            IsActive = isActive,
            AffectedDeviceCodes = new List<string> { "DEVICE-1" }
        });

        Assert.Empty(cache.WarmedScheduleIds);
    }

    [Fact]
    public async Task HandleAsync_CacheFailureIsLoggedAndNotRethrown()
    {
        var cache = new RecordingMobilePlaybackCacheService { ThrowOnInvalidate = true };
        var handler = new ScheduleCacheEventHandler(cache, NullLogger<ScheduleCacheEventHandler>.Instance);

        var exception = await Record.ExceptionAsync(() => handler.HandleAsync(new ScheduleChangedEvent
        {
            ScheduleId = 10,
            ChangeType = ScheduleChangeType.Updated,
            IsActive = true,
            AffectedDeviceCodes = new List<string> { "DEVICE-1" }
        }));

        Assert.Null(exception);
        Assert.Empty(cache.WarmedScheduleIds);
    }

    [Fact]
    public async Task InvalidateDevicePlaybackCachesAsync_RemovesPlaybackAndActiveScheduleKeysForEachDistinctDevice()
    {
        var cache = new RecordingCacheService();
        var service = new MobilePlaybackCacheService(
            cache,
            new EmptyRepository<PlaybackSchedule>(),
            new FixedTimeService());

        await service.InvalidateDevicePlaybackCachesAsync(new[] { " DEV-1 ", "dev-1", "DEV-2", "" });

        Assert.Equal(new[]
        {
            "mobile:playback-state:DEV-1",
            "mobile:device-active-schedule:DEV-1",
            "mobile:playback-state:DEV-2",
            "mobile:device-active-schedule:DEV-2"
        }, cache.RemovedKeys);
    }

    private sealed class RecordingMobilePlaybackCacheService : IMobilePlaybackCacheService
    {
        public bool ThrowOnInvalidate { get; set; }
        public List<string> InvalidatedDeviceCodes { get; } = new();
        public List<int> WarmedScheduleIds { get; } = new();

        public string PlaybackStateKey(string deviceCode) => $"playback:{deviceCode}";
        public string DeviceActiveScheduleKey(string deviceCode) => $"active:{deviceCode}";
        public string ScheduleContentKey(int scheduleId, string version) => $"content:{scheduleId}:{version}";
        public Task<MobileScheduleContentCache> GetOrBuildScheduleContentAsync(PlaybackSchedule schedule, DateTime vietnamNow) => throw new NotSupportedException();
        public Task InvalidateScheduleDevicesAsync(int scheduleId) => throw new NotSupportedException();

        public Task WarmScheduleContentAsync(int scheduleId)
        {
            WarmedScheduleIds.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task InvalidateDevicePlaybackCachesAsync(IEnumerable<string> deviceCodes)
        {
            if (ThrowOnInvalidate)
                throw new InvalidOperationException("cache failure");

            InvalidatedDeviceCodes.AddRange(deviceCodes);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCacheService : ICacheService
    {
        public List<string> RemovedKeys { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyRepository<T> : IRepository<T> where T : class
    {
        public Task<T?> GetByIdAsync(int id) => Task.FromResult<T?>(null);
        public Task<List<T>> ListAsync() => Task.FromResult(new List<T>());
        public IQueryable<T> Query() => Array.Empty<T>().AsQueryable();
        public Task AddAsync(T entity) => Task.CompletedTask;
        public void Update(T entity) { }
        public void Delete(T entity) { }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private sealed class FixedTimeService : ITimeService
    {
        public DateTime UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime ToVietnamTime(DateTime utc) => utc;
        public DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Utc);
    }
}
