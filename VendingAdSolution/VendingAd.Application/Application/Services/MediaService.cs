using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IMediaService
{
    Task<List<Media>> GetAllMediaAsync();
    Task<List<Media>> GetAllMediaWithDetailsAsync(int? userId = null, string? keyword = null);
    Task<Media?> GetMediaWithPlaylistItemsAsync(int mediaId);
    Task<List<Media>> GetUserMediaAsync(int userId, string? keyword = null, string? sortBy = null, bool descending = true);
    Task<List<Media>> GetUserMediaByIdsAsync(IEnumerable<int> ids, int userId);
    Task<Media?> GetByIdAsync(int id);
    Task AddAsync(Media media);
    void Remove(Media media);
    Task SaveChangesAsync();
}

public class MediaService : IMediaService
{
    private readonly IRepository<Media> _medias;

    public MediaService(IRepository<Media> medias)
    {
        _medias = medias;
    }

    public async Task<List<Media>> GetAllMediaAsync()
        => await _medias.Query().AsNoTracking().ToListAsync();

    public async Task<List<Media>> GetAllMediaWithDetailsAsync(int? userId = null, string? keyword = null)
    {
        var query = _medias.Query()
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.PlaylistItems)
                .ThenInclude(pi => pi.Playlist)
            .AsQueryable();

        if (userId.HasValue && userId.Value > 0)
            query = query.Where(m => m.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m =>
                m.FileName.Contains(keyword.Trim()) ||
                m.PlaylistItems.Any(i => i.Playlist.Name.Contains(keyword.Trim())));

        return await query.OrderByDescending(m => m.UploadedAt).ToListAsync();
    }

    public async Task<Media?> GetMediaWithPlaylistItemsAsync(int mediaId)
        => await _medias.Query().Include(m => m.PlaylistItems).FirstOrDefaultAsync(m => m.Id == mediaId);

    public async Task<List<Media>> GetUserMediaAsync(int userId, string? keyword = null, string? sortBy = null, bool descending = true)
    {
        var query = _medias.Query()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Include(m => m.PlaylistItems)
                .ThenInclude(pi => pi.Playlist)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => m.FileName.Contains(keyword.Trim()));

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "filename" => descending ? query.OrderByDescending(m => m.FileName) : query.OrderBy(m => m.FileName),
            "filesize" => descending ? query.OrderByDescending(m => m.FileSize) : query.OrderBy(m => m.FileSize),
            _ => descending ? query.OrderByDescending(m => m.UploadedAt) : query.OrderBy(m => m.UploadedAt)
        };

        return await query.ToListAsync();
    }

    public async Task<List<Media>> GetUserMediaByIdsAsync(IEnumerable<int> ids, int userId)
        => await _medias.Query().Where(m => ids.Contains(m.Id) && m.UserId == userId).ToListAsync();

    public Task<Media?> GetByIdAsync(int id) => _medias.GetByIdAsync(id);
    public Task AddAsync(Media media) => _medias.AddAsync(media);
    public void Remove(Media media) => _medias.Delete(media);
    public async Task SaveChangesAsync() => await _medias.SaveChangesAsync();
}
