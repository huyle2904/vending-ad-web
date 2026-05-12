using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IPlaylistManagementService
{
    Task<PlaylistActionResult> CreateTemplateAsync(string name, int userId);
    Task<Playlist?> GetPlaylistForUserAsync(int playlistId, int userId);
    Task<List<Playlist>> GetPlaylistsForUserAsync(int userId);
    Task<bool> AddMediaToPlaylistAsync(int playlistId, int mediaId, int userId);
    Task<PlaylistActionResult> AddMediasToPlaylistAsync(int playlistId, IEnumerable<int> mediaIds, int userId);
    Task<bool> UpdatePlaylistOrderAsync(int playlistId, List<PlaylistOrderUpdate> updates, int userId);
    Task<PlaylistActionResult> UpdatePlaylistAsync(UpdatePlaylistRequest request, int userId);
    Task<bool> DeletePlaylistAsync(int playlistId, int userId);
    Task<bool> RemovePlaylistItemAsync(int playlistItemId, int userId);
}

public class PlaylistManagementService : IPlaylistManagementService
{
    private readonly IRepository<Playlist> _playlists;
    private readonly IRepository<PlaylistItem> _playlistItems;
    private readonly IRepository<Media> _medias;
    private readonly ITimeService _timeService;

    public PlaylistManagementService(
        IRepository<Playlist> playlists,
        IRepository<PlaylistItem> playlistItems,
        IRepository<Media> medias,
        ITimeService timeService)
    {
        _playlists = playlists;
        _playlistItems = playlistItems;
        _medias = medias;
        _timeService = timeService;
    }

    public async Task<PlaylistActionResult> CreateTemplateAsync(string name, int userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new PlaylistActionResult { Success = false, Message = "Tên danh sách phát là bắt buộc" };

        var now = _timeService.UtcNow;
        await _playlists.AddAsync(new Playlist
        {
            Name = name.Trim(),
            UserId = userId,
            IsActive = true,
            CreatedAt = now
        });

        await _playlists.SaveChangesAsync();
        return new PlaylistActionResult { Success = true, Message = "Đã tạo danh sách phát" };
    }

    public Task<Playlist?> GetPlaylistForUserAsync(int playlistId, int userId)
    {
        return _playlists.Query()
            .Include(p => p.Items)
                .ThenInclude(pi => pi.Media)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId);
    }

    public Task<List<Playlist>> GetPlaylistsForUserAsync(int userId)
    {
        return _playlists.Query()
            .Include(p => p.Items)
                .ThenInclude(pi => pi.Media)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> AddMediaToPlaylistAsync(int playlistId, int mediaId, int userId)
    {
        var result = await AddMediasToPlaylistAsync(playlistId, new[] { mediaId }, userId);
        return result.Success;
    }

    public async Task<PlaylistActionResult> AddMediasToPlaylistAsync(int playlistId, IEnumerable<int> mediaIds, int userId)
    {
        var playlist = await _playlists.Query().FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId);
        if (playlist == null)
            return new PlaylistActionResult { Success = false, Message = "Không tìm thấy danh sách phát" };

        var ids = mediaIds.Distinct().ToList();
        if (!ids.Any())
            return new PlaylistActionResult { Success = false, Message = "Please select at least one video." };

        var validMediaIds = await _medias.Query()
            .Where(m => ids.Contains(m.Id) && m.UserId == userId)
            .Select(m => m.Id)
            .ToListAsync();

        if (validMediaIds.Count != ids.Count)
            return new PlaylistActionResult { Success = false, Message = "Video đã chọn không hợp lệ" };

        var nextOrder = await _playlistItems.Query()
            .Where(i => i.PlaylistId == playlistId)
            .Select(i => (int?)i.OrderIndex)
            .MaxAsync() ?? -1;

        foreach (var mediaId in validMediaIds)
        {
            nextOrder++;
            await _playlistItems.AddAsync(new PlaylistItem
            {
                PlaylistId = playlistId,
                MediaId = mediaId,
                OrderIndex = nextOrder
            });
        }

        await _playlistItems.SaveChangesAsync();
        return new PlaylistActionResult { Success = true, Message = "Video added to playlist." };
    }

    public async Task<bool> UpdatePlaylistOrderAsync(int playlistId, List<PlaylistOrderUpdate> updates, int userId)
    {
        var playlist = await _playlists.Query().FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId);
        if (playlist == null)
            return false;

        var items = await _playlistItems.Query()
            .Where(i => i.PlaylistId == playlistId)
            .ToListAsync();

        foreach (var update in updates)
        {
            var item = items.FirstOrDefault(i => i.Id == update.PlaylistItemId);
            if (item != null)
                item.OrderIndex = update.OrderIndex;
        }

        await _playlistItems.SaveChangesAsync();
        return true;
    }

    public async Task<PlaylistActionResult> UpdatePlaylistAsync(UpdatePlaylistRequest request, int userId)
    {
        var playlist = await _playlists.Query()
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == userId);

        if (playlist == null)
            return new PlaylistActionResult { Success = false, Message = "Không tìm thấy danh sách phát" };

        if (string.IsNullOrWhiteSpace(request.Name))
            return new PlaylistActionResult { Success = false, Message = "Tên danh sách phát là bắt buộc" };

        playlist.Name = request.Name.Trim();
        playlist.IsActive = request.IsActive;

        await _playlists.SaveChangesAsync();
        return new PlaylistActionResult { Success = true, Message = "Đã cập nhật danh sách phát" };
    }

    public async Task<bool> DeletePlaylistAsync(int playlistId, int userId)
    {
        var playlist = await _playlists.Query().FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId);
        if (playlist == null)
            return false;

        _playlists.Delete(playlist);
        await _playlists.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemovePlaylistItemAsync(int playlistItemId, int userId)
    {
        var item = await _playlistItems.Query()
            .Include(i => i.Playlist)
            .FirstOrDefaultAsync(i => i.Id == playlistItemId && i.Playlist.UserId == userId);

        if (item == null)
            return false;

        _playlistItems.Delete(item);
        await _playlistItems.SaveChangesAsync();
        return true;
    }
}
