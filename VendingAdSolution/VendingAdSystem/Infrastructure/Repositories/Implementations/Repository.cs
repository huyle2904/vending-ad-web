using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Infrastructure.Repositories.Implementations;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id) => _dbSet.FindAsync(id).AsTask();
    public Task<List<T>> ListAsync() => _dbSet.ToListAsync();
    public IQueryable<T> Query() => _dbSet.AsQueryable();
    public Task AddAsync(T entity) => _dbSet.AddAsync(entity).AsTask();
    public void Update(T entity) => _dbSet.Update(entity);
    public void Delete(T entity) => _dbSet.Remove(entity);
    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
