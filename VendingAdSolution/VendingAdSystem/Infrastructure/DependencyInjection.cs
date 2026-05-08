using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentSession, CurrentSession>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
