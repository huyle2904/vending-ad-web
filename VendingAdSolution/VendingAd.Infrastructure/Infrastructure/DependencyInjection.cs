using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using VendingAdSystem.Application.Messaging;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Infrastructure.Caching;
using VendingAdSystem.Infrastructure.Health;
using VendingAdSystem.Infrastructure.Messaging;
using VendingAdSystem.Infrastructure.Persistence;
using VendingAdSystem.Infrastructure.Repositories.Implementations;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;
using VendingAdSystem.Infrastructure.Storage;

namespace VendingAdSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddCache(configuration, requireRedis: false);
        services.AddVendingAdHealthChecks();
        services.Configure<DevicePresenceOptions>(configuration.GetSection("DevicePresence"));
        services.Configure<MobileRateLimitOptions>(configuration.GetSection("MobileRateLimiting"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));

        services.AddSingleton<IApplicationMetrics, NullApplicationMetrics>();
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICurrentSession, CurrentSession>();
        services.AddScoped<ITimeService, TimeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceCredentialService, DeviceCredentialService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaUploadService, MediaUploadService>();
        services.AddScoped<IPlaylistService, PlaylistService>();
        services.AddScoped<IPlaylistManagementService, PlaylistManagementService>();
        services.AddScoped<IPlaybackScheduleService, PlaybackScheduleService>();
        services.AddScoped<IMobilePlaybackService, MobilePlaybackService>();
        services.AddScoped<IMobilePlaybackCacheService, MobilePlaybackCacheService>();
        services.AddScoped<IDevicePresenceService, DevicePresenceService>();
        services.AddScoped<IScheduleCacheEventHandler, ScheduleCacheEventHandler>();
        services.AddSingleton<IMobileRateLimitService, MobileRateLimitService>();
        if (configuration.GetValue<bool>("RabbitMQ:Enabled"))
            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        else
            services.AddSingleton<IMessagePublisher, NullMessagePublisher>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }

    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddCache(configuration, requireRedis: true);
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));

        services.AddSingleton<IApplicationMetrics, NullApplicationMetrics>();
        services.AddScoped<ITimeService, TimeService>();
        services.AddScoped<IMobilePlaybackCacheService, MobilePlaybackCacheService>();
        services.AddScoped<IScheduleCacheEventHandler, ScheduleCacheEventHandler>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddHostedService<WorkerStartupDependencyValidator>();

        return services;
    }

    private static IServiceCollection AddVendingAdHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application process is running."), tags: new[] { "live" })
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" })
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string is missing. Set DATABASE_URL env var or DefaultConnection in config.");
        var normalizedConnectionString = PostgresConnectionStringResolver.Normalize(connectionString);

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(normalizedConnectionString);
        if (!builder.ContainsKey("Maximum Pool Size"))
            builder["Maximum Pool Size"] = 10;
        if (!builder.ContainsKey("Timeout"))
            builder["Timeout"] = 15;

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.ConnectionString));

        return services;
    }

    private static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration, bool requireRedis)
    {
        services.AddMemoryCache();

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddScoped<ICacheService, RedisCacheService>();
            return services;
        }

        if (requireRedis)
            throw new InvalidOperationException("Redis must be enabled for worker-driven cache invalidation.");

        services.AddScoped<ICacheService, MemoryCacheService>();
        return services;
    }
}

