using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace VendingAdSystem.Infrastructure.Health;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public RedisHealthCheck(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var redisEnabled = _configuration.GetValue<bool>("Redis:Enabled");
        if (!redisEnabled)
            return HealthCheckResult.Healthy("Redis is disabled by configuration.");

        var connectionString = _configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Unhealthy("Redis is enabled but Redis:ConnectionString is missing.");

        try
        {
            var multiplexer = _serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            var latency = await multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis ping succeeded in {latency.TotalMilliseconds:N0} ms.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
        }
    }
}
