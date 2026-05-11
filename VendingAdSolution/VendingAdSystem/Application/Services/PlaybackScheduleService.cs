using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IPlaybackScheduleService
{
    Task<IEnumerable<PlaybackSchedule>> GetAllAsync();
    Task<PlaybackSchedule?> GetByIdAsync(int id);
    Task<List<PlaybackSchedule>> GetForUserAsync(int userId);
    Task<PlaybackScheduleActionResult> CreateAsync(int userId, PlaybackScheduleRequest request);
    Task<PlaybackScheduleActionResult> CreateImmediateAsync(int userId, PlaybackScheduleRequest request);
    Task<PlaybackScheduleActionResult> UpdateAsync(int userId, PlaybackScheduleRequest request);
    Task<bool> DeleteAsync(int userId, int scheduleId);
    Task<bool> ToggleAsync(int userId, int scheduleId);
    Task<bool> DeleteByIdAsync(int scheduleId);
    Task<bool> ToggleByIdAsync(int scheduleId);
    Task<PlaybackScheduleActionResult> AddItemAsync(int userId, int scheduleId, int mediaId);
    Task<bool> RemoveItemAsync(int userId, int scheduleItemId);
    Task<bool> UpdateItemOrderAsync(int userId, int scheduleId, List<PlaybackScheduleItemOrderUpdate> updates);
}

public class PlaybackScheduleService : IPlaybackScheduleService
{
    private readonly IRepository<PlaybackSchedule> _playbackScheduleRepository;
    private readonly IRepository<PlaybackScheduleDevice> _scheduleDevices;
    private readonly IRepository<PlaybackScheduleItem> _scheduleItems;
    private readonly IRepository<Device> _devices;
    private readonly IRepository<Playlist> _playlists;
    private readonly IRepository<Media> _medias;
    private readonly ITimeService _timeService;

    public PlaybackScheduleService(
        IRepository<PlaybackSchedule> playbackScheduleRepository,
        IRepository<PlaybackScheduleDevice> scheduleDevices,
        IRepository<PlaybackScheduleItem> scheduleItems,
        IRepository<Device> devices,
        IRepository<Playlist> playlists,
        IRepository<Media> medias,
        ITimeService timeService)
    {
        _playbackScheduleRepository = playbackScheduleRepository;
        _scheduleDevices = scheduleDevices;
        _scheduleItems = scheduleItems;
        _devices = devices;
        _playlists = playlists;
        _medias = medias;
        _timeService = timeService;
    }

    public Task<IEnumerable<PlaybackSchedule>> GetAllAsync()
    {
        return _playbackScheduleRepository.Query()
            .Include(s => s.Devices).ThenInclude(d => d.Device)
            .Include(s => s.Items).ThenInclude(i => i.Media)
            .ToListAsync()
            .ContinueWith(t => (IEnumerable<PlaybackSchedule>)t.Result);
    }

    public Task<PlaybackSchedule?> GetByIdAsync(int id)
    {
        return _playbackScheduleRepository.GetByIdAsync(id);
    }

    public Task<List<PlaybackSchedule>> GetForUserAsync(int userId)
    {
        return _playbackScheduleRepository.Query()
            .Where(s => s.UserId == userId)
            .Include(s => s.Devices).ThenInclude(d => d.Device)
            .Include(s => s.Items).ThenInclude(i => i.Media)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<PlaybackScheduleActionResult> CreateAsync(int userId, PlaybackScheduleRequest request)
    {
        var validation = await ValidateRequestAsync(userId, request, null);
        if (!validation.Success)
            return validation;

        var schedule = new PlaybackSchedule
        {
            Name = request.Name.Trim(),
            UserId = userId,
            StartDate = _timeService.ToUtc(request.StartDate.Date),
            EndDate = _timeService.ToUtc(request.EndDate.Date.AddDays(1).AddTicks(-1)),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = true,
            CreatedAt = _timeService.UtcNow
        };

        await _playbackScheduleRepository.AddAsync(schedule);
        await _playbackScheduleRepository.SaveChangesAsync();

        foreach (var deviceId in request.DeviceIds.Distinct())
            await _scheduleDevices.AddAsync(new PlaybackScheduleDevice { PlaybackScheduleId = schedule.Id, DeviceId = deviceId });

        if (request.MediaIds.Any())
        {
            for (var i = 0; i < request.MediaIds.Count; i++)
                await _scheduleItems.AddAsync(new PlaybackScheduleItem { PlaybackScheduleId = schedule.Id, MediaId = request.MediaIds[i], OrderIndex = i });
        }
        else if (request.PlaylistId.HasValue)
        {
            var playlist = await _playlists.Query().Include(p => p.Items).FirstAsync(p => p.Id == request.PlaylistId.Value);
            var ordered = playlist.Items.OrderBy(i => i.OrderIndex).ToList();
            for (var i = 0; i < ordered.Count; i++)
                await _scheduleItems.AddAsync(new PlaybackScheduleItem { PlaybackScheduleId = schedule.Id, MediaId = ordered[i].MediaId, OrderIndex = i });
        }

        await _playbackScheduleRepository.SaveChangesAsync();
        return new PlaybackScheduleActionResult { Success = true, Message = "Schedule created successfully." };
    }

    public async Task<PlaybackScheduleActionResult> CreateImmediateAsync(int userId, PlaybackScheduleRequest request)
    {
        var validation = await ValidateRequestAsync(userId, request, null, true);
        if (!validation.Success)
            return validation;

        var now = _timeService.UtcNow;
        var schedule = new PlaybackSchedule
        {
            Name = request.Name.Trim(),
            UserId = userId,
            StartDate = _timeService.ToUtc(request.StartDate.Date),
            EndDate = _timeService.ToUtc(request.EndDate.Date.AddDays(1).AddTicks(-1)),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = true,
            IsImmediate = true,
            ImmediateStartedAt = now,
            CreatedAt = now
        };

        await _playbackScheduleRepository.AddAsync(schedule);
        await _playbackScheduleRepository.SaveChangesAsync();
        await AddScheduleLinksAsync(schedule.Id, request);
        return new PlaybackScheduleActionResult { Success = true, Message = "Immediate playback started." };
    }

    public async Task<PlaybackScheduleActionResult> UpdateAsync(int userId, PlaybackScheduleRequest request)
    {
        var schedule = await _playbackScheduleRepository.Query()
            .Include(s => s.Devices)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.UserId == userId);

        if (schedule == null)
            return new PlaybackScheduleActionResult { Success = false, Message = "Schedule not found." };

        var validation = await ValidateRequestAsync(userId, request, schedule.Id);
        if (!validation.Success)
            return validation;

        schedule.Name = request.Name.Trim();
        schedule.StartDate = _timeService.ToUtc(request.StartDate.Date);
        schedule.EndDate = _timeService.ToUtc(request.EndDate.Date.AddDays(1).AddTicks(-1));
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.IsActive = request.IsActive;

        foreach (var device in schedule.Devices.ToList())
            _scheduleDevices.Delete(device);
        foreach (var item in schedule.Items.ToList())
            _scheduleItems.Delete(item);

        await _playbackScheduleRepository.SaveChangesAsync();

        foreach (var deviceId in request.DeviceIds.Distinct())
            await _scheduleDevices.AddAsync(new PlaybackScheduleDevice { PlaybackScheduleId = schedule.Id, DeviceId = deviceId });

        if (request.MediaIds.Any())
        {
            for (var i = 0; i < request.MediaIds.Count; i++)
                await _scheduleItems.AddAsync(new PlaybackScheduleItem { PlaybackScheduleId = schedule.Id, MediaId = request.MediaIds[i], OrderIndex = i });
        }
        else if (request.PlaylistId.HasValue)
        {
            var playlist = await _playlists.Query().Include(p => p.Items).FirstAsync(p => p.Id == request.PlaylistId.Value);
            var ordered = playlist.Items.OrderBy(i => i.OrderIndex).ToList();
            for (var i = 0; i < ordered.Count; i++)
                await _scheduleItems.AddAsync(new PlaybackScheduleItem { PlaybackScheduleId = schedule.Id, MediaId = ordered[i].MediaId, OrderIndex = i });
        }

        await _playbackScheduleRepository.SaveChangesAsync();
        return new PlaybackScheduleActionResult { Success = true, Message = "Schedule updated successfully." };
    }

    public async Task<bool> DeleteAsync(int userId, int scheduleId)
    {
        var schedule = await _playbackScheduleRepository.Query().FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId);
        if (schedule == null) return false;
        _playbackScheduleRepository.Delete(schedule);
        await _playbackScheduleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleAsync(int userId, int scheduleId)
    {
        var schedule = await _playbackScheduleRepository.Query().FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId);
        if (schedule == null) return false;
        schedule.IsActive = !schedule.IsActive;
        await _playbackScheduleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteByIdAsync(int scheduleId)
    {
        var schedule = await _playbackScheduleRepository.Query().FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule == null) return false;
        _playbackScheduleRepository.Delete(schedule);
        await _playbackScheduleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleByIdAsync(int scheduleId)
    {
        var schedule = await _playbackScheduleRepository.Query().FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule == null) return false;
        schedule.IsActive = !schedule.IsActive;
        await _playbackScheduleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<PlaybackScheduleActionResult> AddItemAsync(int userId, int scheduleId, int mediaId)
    {
        var schedule = await _playbackScheduleRepository.Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId);

        if (schedule == null)
            return new PlaybackScheduleActionResult { Success = false, Message = "Schedule not found." };

        var media = await _medias.Query().FirstOrDefaultAsync(m => m.Id == mediaId && m.UserId == userId);
        if (media == null)
            return new PlaybackScheduleActionResult { Success = false, Message = "Selected video is invalid." };

        var nextOrder = schedule.Items.Select(i => i.OrderIndex).DefaultIfEmpty(-1).Max() + 1;
        await _scheduleItems.AddAsync(new PlaybackScheduleItem
        {
            PlaybackScheduleId = scheduleId,
            MediaId = mediaId,
            OrderIndex = nextOrder
        });

        await _playbackScheduleRepository.SaveChangesAsync();
        return new PlaybackScheduleActionResult { Success = true, Message = "Video added to schedule." };
    }

    public async Task<bool> RemoveItemAsync(int userId, int scheduleItemId)
    {
        var item = await _scheduleItems.Query()
            .Include(i => i.PlaybackSchedule)
            .FirstOrDefaultAsync(i => i.Id == scheduleItemId && i.PlaybackSchedule.UserId == userId);

        if (item == null)
            return false;

        var scheduleId = item.PlaybackScheduleId;
        _scheduleItems.Delete(item);
        await _scheduleItems.SaveChangesAsync();

        await NormalizeScheduleItemOrderAsync(scheduleId);
        return true;
    }

    public async Task<bool> UpdateItemOrderAsync(int userId, int scheduleId, List<PlaybackScheduleItemOrderUpdate> updates)
    {
        var schedule = await _playbackScheduleRepository.Query()
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId);

        if (schedule == null)
            return false;

        var items = await _scheduleItems.Query()
            .Where(i => i.PlaybackScheduleId == scheduleId)
            .ToListAsync();

        foreach (var update in updates)
        {
            var item = items.FirstOrDefault(i => i.Id == update.ScheduleItemId);
            if (item != null)
                item.OrderIndex = update.OrderIndex;
        }

        await _scheduleItems.SaveChangesAsync();
        await NormalizeScheduleItemOrderAsync(scheduleId);
        return true;
    }

    private async Task<PlaybackScheduleActionResult> ValidateRequestAsync(int userId, PlaybackScheduleRequest request, int? currentId, bool isImmediate = false)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return new PlaybackScheduleActionResult { Success = false, Message = "Name is required." };
        if (request.EndDate < request.StartDate)
            return new PlaybackScheduleActionResult { Success = false, Message = "End date must be after start date." };
        if (request.EndTime <= request.StartTime)
            return new PlaybackScheduleActionResult { Success = false, Message = "No cross-midnight time range." };
        if (!request.DeviceIds.Any())
            return new PlaybackScheduleActionResult { Success = false, Message = "Select at least one device." };
        if (request.PlaylistId == null && !request.MediaIds.Any())
            return new PlaybackScheduleActionResult { Success = false, Message = "Select playlist or media list." };
        if (request.PlaylistId.HasValue && request.MediaIds.Any())
            return new PlaybackScheduleActionResult { Success = false, Message = "Select either playlist or media list, not both." };

        var validDevices = await _devices.Query().CountAsync(d => request.DeviceIds.Contains(d.Id) && d.UserId == userId && d.IsActive);
        if (validDevices != request.DeviceIds.Distinct().Count())
            return new PlaybackScheduleActionResult { Success = false, Message = "Device invalid." };

        var startUtc = _timeService.ToUtc(request.StartDate.Date);
        var endUtc = _timeService.ToUtc(request.EndDate.Date.AddDays(1).AddTicks(-1));
        var overlapCandidates = await _playbackScheduleRepository.Query()
            .Include(s => s.Devices)
            .Where(s => s.UserId == userId && s.IsActive && s.Id != currentId)
            .Where(s => s.StartDate <= endUtc && s.EndDate >= startUtc)
            .ToListAsync();

        var overlap = overlapCandidates.Any(s =>
            s.Devices.Any(d => request.DeviceIds.Contains(d.DeviceId)) &&
            s.StartTime < request.EndTime &&
            request.StartTime < s.EndTime);
        if (overlap)
            return new PlaybackScheduleActionResult { Success = false, Message = isImmediate ? "Thiết bị đang có lịch phát active. Vui lòng dừng hoặc xóa schedule trước khi phát ngay." : "Device already has active schedule in this range." };

        if (request.PlaylistId.HasValue)
        {
            var playlist = await _playlists.Query()
                .Include(p => p.Items)
                    .ThenInclude(i => i.Media)
                .FirstOrDefaultAsync(p => p.Id == request.PlaylistId.Value && p.UserId == userId && p.IsActive);
            if (playlist == null)
                return new PlaybackScheduleActionResult { Success = false, Message = "Playlist not found." };
            if (!playlist.Items.Any())
                return new PlaybackScheduleActionResult { Success = false, Message = "Playlist has no videos." };
            if (playlist.Items.Any(i => i.Media.UserId != userId))
                return new PlaybackScheduleActionResult { Success = false, Message = "Playlist contains invalid videos." };
        }

        if (request.MediaIds.Any())
        {
            var mediaIds = request.MediaIds.Distinct().ToList();
            var validMediaCount = await _medias.Query().CountAsync(m => mediaIds.Contains(m.Id) && m.UserId == userId);
            if (validMediaCount != mediaIds.Count)
                return new PlaybackScheduleActionResult { Success = false, Message = "Selected video is invalid." };
        }

        return new PlaybackScheduleActionResult { Success = true };
    }

    private async Task AddScheduleLinksAsync(int scheduleId, PlaybackScheduleRequest request)
    {
        foreach (var deviceId in request.DeviceIds.Distinct())
            await _scheduleDevices.AddAsync(new PlaybackScheduleDevice { PlaybackScheduleId = scheduleId, DeviceId = deviceId });

        if (request.MediaIds.Any())
        {
            for (var i = 0; i < request.MediaIds.Count; i++)
                await _scheduleItems.AddAsync(new PlaybackScheduleItem { PlaybackScheduleId = scheduleId, MediaId = request.MediaIds[i], OrderIndex = i });
        }
        else if (request.PlaylistId.HasValue)
        {
            var playlist = await _playlists.Query().Include(p => p.Items).FirstAsync(p => p.Id == request.PlaylistId.Value);
            var ordered = playlist.Items.OrderBy(i => i.OrderIndex).ToList();
            for (var i = 0; i < ordered.Count; i++)
                await _scheduleItems.AddAsync(new PlaybackScheduleItem { PlaybackScheduleId = scheduleId, MediaId = ordered[i].MediaId, OrderIndex = i });
        }

        await _playbackScheduleRepository.SaveChangesAsync();
    }

    private async Task NormalizeScheduleItemOrderAsync(int scheduleId)
    {
        var items = await _scheduleItems.Query()
            .Where(i => i.PlaybackScheduleId == scheduleId)
            .OrderBy(i => i.OrderIndex)
            .ThenBy(i => i.Id)
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
            items[i].OrderIndex = i;

        await _scheduleItems.SaveChangesAsync();
    }
}
