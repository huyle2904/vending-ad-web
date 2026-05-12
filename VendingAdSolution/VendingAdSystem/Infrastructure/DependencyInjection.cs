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
        var provider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(NormalizePostgresConnectionString(connectionString));
                return;
            }

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("MSSQL", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
                return;
            }

            options.UseSqlite(connectionString);
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentSession, CurrentSession>();
        services.AddScoped<ITimeService, TimeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaUploadService, MediaUploadService>();
        services.AddScoped<IPlaylistService, PlaylistService>();
        services.AddScoped<IPlaylistManagementService, PlaylistManagementService>();
        services.AddScoped<IPlaybackScheduleService, PlaybackScheduleService>();
        services.AddScoped<IMobilePlaybackService, MobilePlaybackService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }

    private static string NormalizePostgresConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return connectionString;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty);
        var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty);
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
}
