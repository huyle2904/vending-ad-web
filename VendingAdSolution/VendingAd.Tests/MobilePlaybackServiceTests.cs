using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using Xunit;

namespace VendingAd.Tests;

public class MobilePlaybackServiceTests
{
    [Fact]
    public async Task GetPlaybackStateAsync_ActiveSchedule_UsesConfiguredPlaybackStateTtl()
    {
        await using var database = await TestDatabase.CreateAsync(seedActiveSchedule: true);
        var cache = new RecordingCacheService();
        var service = CreateService(database.Context, cache, new MobilePlaybackCacheOptions
        {
            PlaybackStateTtlSeconds = 37,
            DeviceActiveScheduleTtlSeconds = 29
        });

        var response = await service.GetPlaybackStateAsync("TAB-01");

        Assert.NotNull(response);
        Assert.True(response.HasActiveSchedule);
        Assert.Contains(cache.SetCalls, call => call.Key == "mobile:playback-state:TAB-01" && call.Ttl == TimeSpan.FromSeconds(37));
        Assert.Contains(cache.SetCalls, call => call.Key == "mobile:device-active-schedule:TAB-01" && call.Ttl == TimeSpan.FromSeconds(29));
    }

    [Fact]
    public async Task GetPlaybackStateAsync_NoActiveSchedule_UsesConfiguredNoActiveScheduleTtl()
    {
        await using var database = await TestDatabase.CreateAsync(seedActiveSchedule: false);
        var cache = new RecordingCacheService();
        var service = CreateService(database.Context, cache, new MobilePlaybackCacheOptions
        {
            NoActiveScheduleTtlSeconds = 14
        });

        var response = await service.GetPlaybackStateAsync("TAB-01");

        Assert.NotNull(response);
        Assert.False(response.HasActiveSchedule);
        Assert.Contains(cache.SetCalls, call => call.Key == "mobile:playback-state:TAB-01" && call.Ttl == TimeSpan.FromSeconds(14));
    }

    [Theory]
    [InlineData(false, 123, 21)]
    [InlineData(true, null, 65)]
    public async Task GetPlaybackStateAsync_InactiveOrUnclaimedDevice_UsesConfiguredTtl(bool isActive, int? userId, int expectedTtlSeconds)
    {
        await using var database = await TestDatabase.CreateAsync(seedActiveSchedule: false, isActive: isActive, userId: userId);
        var cache = new RecordingCacheService();
        var service = CreateService(database.Context, cache, new MobilePlaybackCacheOptions
        {
            InactiveDeviceTtlSeconds = 21,
            UnclaimedDeviceTtlSeconds = 65
        });

        var response = await service.GetPlaybackStateAsync("TAB-01");

        Assert.NotNull(response);
        Assert.False(response.HasActiveSchedule);
        Assert.Contains(cache.SetCalls, call => call.Key == "mobile:playback-state:TAB-01" && call.Ttl == TimeSpan.FromSeconds(expectedTtlSeconds));
    }

    private static MobilePlaybackService CreateService(AppDbContext context, ICacheService cache, MobilePlaybackCacheOptions options)
    {
        return new MobilePlaybackService(
            new Repository<Device>(context),
            new PlaybackScheduleResolver(new Repository<PlaybackSchedule>(context), new FixedTimeService()),
            new FixedTimeService(),
            cache,
            new MobilePlaybackCacheService(cache, new Repository<PlaybackSchedule>(context), new FixedTimeService(), new NullApplicationMetrics()),
            new DevicePresenceService(cache, Options.Create(new DevicePresenceOptions())),
            new NullApplicationMetrics(),
            Options.Create(options));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(AppDbContext context)
        {
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync(bool seedActiveSchedule, bool isActive = true, int? userId = 123)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"mobile-playback-{Guid.NewGuid():N}")
                .Options;

            var context = new AppDbContext(options);
            await SeedAsync(context, seedActiveSchedule, isActive, userId);
            return new TestDatabase(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }

        private static async Task SeedAsync(AppDbContext context, bool seedActiveSchedule, bool isActive, int? userId)
        {
            if (userId.HasValue)
            {
                context.Users.Add(new User
                {
                    Id = userId.Value,
                    Username = "owner",
                    Email = "owner@example.com",
                    PasswordHash = "hash",
                    FullName = "Owner"
                });
            }

            context.Devices.Add(new Device
            {
                Id = 10,
                DeviceCode = "TAB-01",
                DeviceName = "Tablet 01",
                UserId = userId,
                IsActive = isActive,
                ClaimCode = userId.HasValue ? null : "CLAIM-01"
            });

            if (seedActiveSchedule)
            {
                context.Medias.Add(new Media
                {
                    Id = 20,
                    FileName = "clip.mp4",
                    FileUrl = "/uploads/clip.mp4",
                    FileSize = 1024,
                    UserId = userId
                });

                context.PlaybackSchedules.Add(new PlaybackSchedule
                {
                    Id = 30,
                    Name = "Morning ads",
                    UserId = userId!.Value,
                    IsActive = true,
                    StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 1, 1, 23, 59, 59, DateTimeKind.Utc),
                    StartTime = TimeSpan.Zero,
                    EndTime = TimeSpan.FromHours(23),
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Devices = new List<PlaybackScheduleDevice>
                    {
                        new() { DeviceId = 10 }
                    },
                    Items = new List<PlaybackScheduleItem>
                    {
                        new() { MediaId = 20, OrderIndex = 0 }
                    }
                });
            }

            await context.SaveChangesAsync();
        }
    }

    private sealed class RecordingCacheService : ICacheService
    {
        public List<CacheSetCall> SetCalls { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            SetCalls.Add(new CacheSetCall(key, ttl));
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record CacheSetCall(string Key, TimeSpan Ttl);

    private sealed class FixedTimeService : ITimeService
    {
        public DateTime UtcNow { get; } = new(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        public DateTime ToVietnamTime(DateTime utc) => utc;
        public DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Utc);
    }
}
