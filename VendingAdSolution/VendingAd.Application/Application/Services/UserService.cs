using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IUserService
{
    Task<List<User>> GetAllUsersAsync();
    Task<List<User>> GetAllUsersSortedAsync();
    Task<List<User>> GetAllActiveUsersAsync();
    Task<int> GetTotalUserCountAsync();
    Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null);
    Task<User?> FindByLoginAsync(string login);
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task AddAsync(User user);
    void Remove(User user);
    Task SaveChangesAsync();
}

public class UserService : IUserService
{
    private readonly IRepository<User> _users;

    public UserService(IRepository<User> users)
    {
        _users = users;
    }

    public async Task<List<User>> GetAllUsersAsync()
        => await _users.Query().AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync();

    public async Task<List<User>> GetAllUsersSortedAsync()
        => await _users.Query().AsNoTracking().OrderBy(u => u.Username).ToListAsync();

    public async Task<List<User>> GetAllActiveUsersAsync()
        => await _users.Query().AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Username).ToListAsync();

    public Task<int> GetTotalUserCountAsync()
        => _users.Query().CountAsync();

    public async Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null)
    {
        var query = _users.Query().Where(u => u.Username == username);
        if (excludeUserId.HasValue)
            query = query.Where(u => u.Id != excludeUserId.Value);
        return await query.AnyAsync();
    }

    public Task<User?> FindByLoginAsync(string login)
        => _users.Query().FirstOrDefaultAsync(u => u.Username == login || u.Email == login);

    public Task<User?> GetByIdAsync(int id) => _users.GetByIdAsync(id);
    public Task<User?> GetByUsernameAsync(string username) => _users.Query().FirstOrDefaultAsync(u => u.Username == username);
    public Task AddAsync(User user) => _users.AddAsync(user);
    public void Remove(User user) => _users.Delete(user);
    public async Task SaveChangesAsync() => await _users.SaveChangesAsync();
}
