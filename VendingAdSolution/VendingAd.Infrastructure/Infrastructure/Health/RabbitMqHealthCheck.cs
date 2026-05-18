using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using VendingAdSystem.Application.Messaging;

namespace VendingAdSystem.Infrastructure.Health;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var rabbitMqEnabled = _configuration.GetValue<bool>("RabbitMQ:Enabled");
        if (!rabbitMqEnabled)
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ is disabled by configuration."));

        var options = new RabbitMqOptions();
        _configuration.GetSection("RabbitMQ").Bind(options);

        try
        {
            using var connection = CreateConnection(options);
            using var channel = connection.CreateModel();
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ connection is healthy."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ health check failed.", ex));
        }
    }

    internal static IConnection CreateConnection(RabbitMqOptions options)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            DispatchConsumersAsync = true
        };

        return factory.CreateConnection();
    }
}
