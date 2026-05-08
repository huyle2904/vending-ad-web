using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IMediaService
{
    IQueryable<Media> Query();
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

    public IQueryable<Media> Query() => _medias.Query();
    public Task<Media?> GetByIdAsync(int id) => _medias.GetByIdAsync(id);
    public Task AddAsync(Media media) => _medias.AddAsync(media);
    public void Remove(Media media) => _medias.Delete(media);
    public async Task SaveChangesAsync() => await _medias.SaveChangesAsync();
}
