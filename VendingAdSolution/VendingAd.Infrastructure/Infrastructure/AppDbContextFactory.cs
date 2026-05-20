using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Infrastructure;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new AppDbContext(optionsBuilder.Options);
    }
}
