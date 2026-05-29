using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using Xunit;

namespace VendingAd.Tests;

public class PlaybackScheduleResolverTests
{
    [Fact]
    public async Task ResolveCurrentForDeviceCodeAsync_FiltersByDeviceBeforeMaterializing()
    {
        await using var database = await TestDatabase.CreateAsync();
        var resolver = CreateResolver(database.Context);

        var schedule = await resolver.ResolveCurrentForDeviceCodeAsync("TAB-01", FixedTimeService.FixedUtcNow);

        Assert.NotNull(schedule);
        Assert.Equal("Immediate active", schedule.Name);
        Assert.All(schedule.Devices, device => Assert.Equal("TAB-01", device.Device.DeviceCode));
    }

    [Fact]
    public async Task ResolveCurrentForDeviceAsync_ImmediateScheduleWinsOverOlderNormalSchedule()
    {
        await using var database = await TestDatabase.CreateAsync();
        var resolver = CreateResolver(database.Context);
        var schedules = await database.Context.PlaybackSchedules
            .Include(s => s.Devices).ThenInclude(d => d.Device)
            .Include(s => s.Items).ThenInclude(i => i.Media)
            .ToListAsync();

        var schedule = resolver.ResolveCurrentForDevice(schedules, deviceId: 1, FixedTimeService.FixedUtcNow);

        Assert.NotNull(schedule);
        Assert.Equal("Immediate active", schedule.Name);
    }

    [Fact]
    public async Task ResolveCurrentForDeviceAsync_ImmediateStartedTodayIgnoresPastStartTimeUntilEndTime()
    {
        await using var database = await TestDatabase.CreateAsync(includeOnlyTodayImmediate: true);
        var resolver = CreateResolver(database.Context);

        var schedule = await resolver.ResolveCurrentForDeviceAsync(1, FixedTimeService.FixedUtcNow);

        Assert.NotNull(schedule);
        Assert.Equal("Started today immediate", schedule.Name);
    }

    [Fact]
    public async Task ResolveUpcomingForDevice_ReturnsEarliestFutureStart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var resolver = CreateResolver(database.Context);
        var schedules = await database.Context.PlaybackSchedules
            .Include(s => s.Devices).ThenInclude(d => d.Device)
            .Include(s => s.Items).ThenInclude(i => i.Media)
            .ToListAsync();

        var schedule = resolver.ResolveUpcomingForDevice(schedules, deviceId: 1, FixedTimeService.FixedUtcNow, excludingScheduleId: 3);

        Assert.NotNull(schedule);
        Assert.Equal("Later today", schedule.Name);
    }

    [Fact]
    public async Task ResolveCurrentForDeviceAsync_ReturnsNullWhenNoActiveSchedule()
    {
        await using var database = await TestDatabase.CreateAsync(seedActive: false);
        var resolver = CreateResolver(database.Context);

        var schedule = await resolver.ResolveCurrentForDeviceAsync(1, FixedTimeService.FixedUtcNow);

        Assert.Null(schedule);
    }

    private static PlaybackScheduleResolver CreateResolver(AppDbContext context)
    {
        return new PlaybackScheduleResolver(new Repository<PlaybackSchedule>(context), new FixedTimeService());
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(AppDbContext context)
        {
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync(bool seedActive = true, bool includeOnlyTodayImmediate = false)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"playback-schedule-resolver-{Guid.NewGuid():N}")
                .Options;
            var context = new AppDbContext(options);
            await SeedAsync(context, seedActive, includeOnlyTodayImmediate);
            return new TestDatabase(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }

        private static async Task SeedAsync(AppDbContext context, bool seedActive, bool includeOnlyTodayImmediate)
        {
            context.Users.Add(new User
            {
                Id = 1,
                Username = "owner",
                Email = "owner@example.com",
                PasswordHash = "hash",
                FullName = "Owner"
            });

            context.Devices.AddRange(
                new Device { Id = 1, DeviceCode = "TAB-01", DeviceName = "Tablet 01", UserId = 1, IsActive = true },
                new Device { Id = 2, DeviceCode = "TAB-02", DeviceName = "Tablet 02", UserId = 1, IsActive = true });

            context.Medias.Add(new Media
            {
                Id = 10,
                FileName = "clip.mp4",
                FileUrl = "/uploads/clip.mp4",
                FileSize = 1024,
                UserId = 1
            });

            if (includeOnlyTodayImmediate)
            {
                context.PlaybackSchedules.Add(CreateSchedule(
                    id: 20,
                    name: "Started today immediate",
                    deviceId: 1,
                    startTime: TimeSpan.FromHours(2),
                    endTime: TimeSpan.FromHours(12),
                    isImmediate: true,
                    immediateStartedAt: FixedTimeService.FixedUtcNow.Date.AddHours(1)));
            }
            else if (seedActive)
            {
                context.PlaybackSchedules.AddRange(
                    CreateSchedule(1, "Assigned active", 1, TimeSpan.Zero, TimeSpan.FromHours(23)),
                    CreateSchedule(2, "Other device active", 2, TimeSpan.Zero, TimeSpan.FromHours(23)),
                    CreateSchedule(3, "Immediate active", 1, TimeSpan.Zero, TimeSpan.FromHours(23), isImmediate: true, immediateStartedAt: FixedTimeService.FixedUtcNow),
                    CreateSchedule(4, "Later today", 1, TimeSpan.FromHours(9), TimeSpan.FromHours(10)),
                    CreateSchedule(5, "Tomorrow", 1, TimeSpan.FromHours(6), TimeSpan.FromHours(7), startOffsetDays: 1, endOffsetDays: 1));
            }
            else
            {
                context.PlaybackSchedules.Add(CreateSchedule(30, "Inactive", 1, TimeSpan.Zero, TimeSpan.FromHours(23), isActive: false));
            }

            await context.SaveChangesAsync();
        }

        private static PlaybackSchedule CreateSchedule(
            int id,
            string name,
            int deviceId,
            TimeSpan startTime,
            TimeSpan endTime,
            bool isActive = true,
            bool isImmediate = false,
            DateTime? immediateStartedAt = null,
            int startOffsetDays = 0,
            int endOffsetDays = 0)
        {
            return new PlaybackSchedule
            {
                Id = id,
                Name = name,
                UserId = 1,
                IsActive = isActive,
                IsImmediate = isImmediate,
                ImmediateStartedAt = immediateStartedAt,
                StartDate = FixedTimeService.FixedUtcNow.Date.AddDays(startOffsetDays),
                EndDate = FixedTimeService.FixedUtcNow.Date.AddDays(endOffsetDays).AddHours(23).AddMinutes(59),
                StartTime = startTime,
                EndTime = endTime,
                CreatedAt = FixedTimeService.FixedUtcNow.AddMinutes(id),
                Devices = new List<PlaybackScheduleDevice>
                {
                    new() { DeviceId = deviceId }
                },
                Items = new List<PlaybackScheduleItem>
                {
                    new() { MediaId = 10, OrderIndex = 0 }
                }
            };
        }
    }

    private sealed class FixedTimeService : ITimeService
    {
        public static DateTime FixedUtcNow { get; } = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => FixedUtcNow;
        public DateTime ToVietnamTime(DateTime utc) => utc;
        public DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Utc);
    }
}
