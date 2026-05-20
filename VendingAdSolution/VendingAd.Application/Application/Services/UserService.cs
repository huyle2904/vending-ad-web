using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IUserService
{
    IQueryable<User> Query();
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

    public IQueryable<User> Query() => _users.Query();
    public Task<User?> GetByIdAsync(int id) => _users.GetByIdAsync(id);
    public Task<User?> GetByUsernameAsync(string username) => _users.Query().FirstOrDefaultAsync(u => u.Username == username);
    public Task AddAsync(User user) => _users.AddAsync(user);
    public void Remove(User user) => _users.Delete(user);
    public async Task SaveChangesAsync() => await _users.SaveChangesAsync();
}
