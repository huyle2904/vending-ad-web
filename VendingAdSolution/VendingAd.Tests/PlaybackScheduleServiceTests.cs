using Microsoft.EntityFrameworkCore;
using VendingAd.Contracts;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Messaging;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using Xunit;

namespace VendingAd.Tests;

public class PlaybackScheduleServiceTests
{
    [Fact]
    public async Task CreateAsync_PublishesCreatedEventForAssignedDevices()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingMessagePublisher();
        var service = CreateService(database.Context, publisher);

        var result = await service.CreateAsync(1, new PlaybackScheduleRequest
        {
            Name = "New schedule",
            StartDate = new DateTime(2026, 1, 3),
            EndDate = new DateTime(2026, 1, 3),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            IsActive = true,
            DeviceIds = new List<int> { 1, 2 },
            MediaIds = new List<int> { 10 }
        });

        Assert.True(result.Success);
        var message = Assert.Single(publisher.PublishedEvents.OfType<ScheduleChangedEvent>());
        Assert.Equal(ScheduleChangeType.Created, message.ChangeType);
        Assert.Equal(new[] { "DEVICE-NEW", "DEVICE-OLD" }, message.AffectedDeviceCodes.OrderBy(code => code));
        Assert.True(message.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WhenReassigningDevices_PublishesUnionOfOldAndNewDeviceCodes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingMessagePublisher();
        var auditService = new RecordingAuditService();
        var service = CreateService(database.Context, publisher, auditService);

        var result = await service.UpdateAsync(1, new PlaybackScheduleRequest
        {
            Id = 100,
            Name = "Updated schedule",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 2),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            IsActive = true,
            DeviceIds = new List<int> { 2 },
            MediaIds = new List<int> { 10 }
        });

        Assert.True(result.Success);
        var message = Assert.Single(publisher.PublishedEvents.OfType<ScheduleChangedEvent>());
        Assert.Equal(ScheduleChangeType.Updated, message.ChangeType);
        Assert.Equal(new[] { "DEVICE-NEW", "DEVICE-OLD" }, message.AffectedDeviceCodes.OrderBy(code => code));
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditActions.UpdateSchedule, auditEntry.Action);
        Assert.Equal(AuditTargets.PlaybackSchedule, auditEntry.TargetType);
        Assert.Equal(100, auditEntry.TargetId);
    }

    [Theory]
    [InlineData(ScheduleChangeType.Deleted)]
    [InlineData(ScheduleChangeType.Toggled)]
    public async Task DeleteAndToggle_PublishEventsForCurrentDevices(ScheduleChangeType expectedChangeType)
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingMessagePublisher();
        var service = CreateService(database.Context, publisher);

        var success = expectedChangeType == ScheduleChangeType.Deleted
            ? await service.DeleteAsync(1, 100)
            : await service.ToggleAsync(1, 100);

        Assert.True(success);
        var message = Assert.Single(publisher.PublishedEvents.OfType<ScheduleChangedEvent>());
        Assert.Equal(expectedChangeType, message.ChangeType);
        Assert.Equal(new[] { "DEVICE-OLD" }, message.AffectedDeviceCodes);
    }

    [Fact]
    public async Task AddItemAsync_PublishesItemAddedEventForScheduleDevices()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingMessagePublisher();
        var service = CreateService(database.Context, publisher);

        var result = await service.AddItemAsync(1, 100, 11);

        Assert.True(result.Success);
        var message = Assert.Single(publisher.PublishedEvents.OfType<ScheduleChangedEvent>());
        Assert.Equal(ScheduleChangeType.ItemAdded, message.ChangeType);
        Assert.Equal(new[] { "DEVICE-OLD" }, message.AffectedDeviceCodes);
    }

    [Fact]
    public async Task RemoveItemAsync_PublishesItemRemovedEventForScheduleDevices()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingMessagePublisher();
        var service = CreateService(database.Context, publisher);

        var success = await service.RemoveItemAsync(1, 1000);

        Assert.True(success);
        var message = Assert.Single(publisher.PublishedEvents.OfType<ScheduleChangedEvent>());
        Assert.Equal(ScheduleChangeType.ItemRemoved, message.ChangeType);
        Assert.Equal(new[] { "DEVICE-OLD" }, message.AffectedDeviceCodes);
    }

    [Fact]
    public async Task UpdateItemOrderAsync_PublishesReorderedEventForScheduleDevices()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingMessagePublisher();
        var service = CreateService(database.Context, publisher);

        var success = await service.UpdateItemOrderAsync(1, 100, new List<PlaybackScheduleItemOrderUpdate>
        {
            new() { ScheduleItemId = 1000, OrderIndex = 1 },
            new() { ScheduleItemId = 1001, OrderIndex = 0 }
        });

        Assert.True(success);
        var message = Assert.Single(publisher.PublishedEvents.OfType<ScheduleChangedEvent>());
        Assert.Equal(ScheduleChangeType.Reordered, message.ChangeType);
        Assert.Equal(new[] { "DEVICE-OLD" }, message.AffectedDeviceCodes);
    }

    private static PlaybackScheduleService CreateService(AppDbContext context, IMessagePublisher publisher, IAuditService? auditService = null)
    {
        return new PlaybackScheduleService(
            new Repository<PlaybackSchedule>(context),
            new Repository<PlaybackScheduleDevice>(context),
            new Repository<PlaybackScheduleItem>(context),
            new Repository<Device>(context),
            new Repository<Playlist>(context),
            new Repository<Media>(context),
            new FixedTimeService(),
            publisher,
            auditService ?? NullAuditService.Instance);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(AppDbContext context)
        {
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"playback-schedules-{Guid.NewGuid():N}")
                .Options;

            var context = new AppDbContext(options);
            await SeedAsync(context);

            return new TestDatabase(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }

        private static async Task SeedAsync(AppDbContext context)
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
                new Device { Id = 1, DeviceCode = "DEVICE-OLD", DeviceName = "Device Old", UserId = 1, IsActive = true },
                new Device { Id = 2, DeviceCode = "DEVICE-NEW", DeviceName = "Device New", UserId = 1, IsActive = true });

            context.Medias.AddRange(
                new Media
                {
                    Id = 10,
                    FileName = "clip.mp4",
                    FileUrl = "/media/clip.mp4",
                    FileSize = 1024,
                    UserId = 1
                },
                new Media
                {
                    Id = 11,
                    FileName = "clip-2.mp4",
                    FileUrl = "/media/clip-2.mp4",
                    FileSize = 2048,
                    UserId = 1
                });

            context.PlaybackSchedules.Add(new PlaybackSchedule
            {
                Id = 100,
                UserId = 1,
                Name = "Existing schedule",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 1, 2),
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(10),
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Devices = new List<PlaybackScheduleDevice>
                {
                    new() { DeviceId = 1 }
                },
                Items = new List<PlaybackScheduleItem>
                {
                    new() { Id = 1000, MediaId = 10, OrderIndex = 0 },
                    new() { Id = 1001, MediaId = 11, OrderIndex = 1 }
                }
            });

            await context.SaveChangesAsync();
        }
    }

    private sealed class RecordingMessagePublisher : IMessagePublisher
    {
        public List<object> PublishedEvents { get; } = new();

        public Task PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default) where TEvent : class
        {
            PublishedEvents.Add(eventMessage);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeService : ITimeService
    {
        public DateTime UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime ToVietnamTime(DateTime utc) => utc;
        public DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Utc);
    }
}
