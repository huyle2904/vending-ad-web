using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Infrastructure;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=true");
        return new AppDbContext(optionsBuilder.Options);
    }
}
