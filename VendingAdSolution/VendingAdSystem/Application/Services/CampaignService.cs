using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface ICampaignService
{
    IQueryable<Campaign> Query();
    Task<Campaign?> GetByIdAsync(int id);
    Task AddAsync(Campaign campaign);
    void RemoveRange(IEnumerable<Campaign> campaigns);
    Task SaveChangesAsync();
}

public class CampaignService : ICampaignService
{
    private readonly IRepository<Campaign> _campaigns;

    public CampaignService(IRepository<Campaign> campaigns)
    {
        _campaigns = campaigns;
    }

    public IQueryable<Campaign> Query() => _campaigns.Query();
    public Task<Campaign?> GetByIdAsync(int id) => _campaigns.GetByIdAsync(id);
    public Task AddAsync(Campaign campaign) => _campaigns.AddAsync(campaign);
    public void RemoveRange(IEnumerable<Campaign> campaigns)
    {
        foreach (var campaign in campaigns)
        {
            _campaigns.Delete(campaign);
        }
    }
    public async Task SaveChangesAsync() => await _campaigns.SaveChangesAsync();
}
