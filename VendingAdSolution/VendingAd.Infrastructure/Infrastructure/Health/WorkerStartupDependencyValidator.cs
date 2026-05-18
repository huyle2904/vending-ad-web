using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StackExchange.Redis;
using VendingAdSystem.Application.Messaging;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Infrastructure.Health;

public sealed class WorkerStartupDependencyValidator : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<WorkerStartupDependencyValidator> _logger;

    public WorkerStartupDependencyValidator(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<WorkerStartupDependencyValidator> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            throw new InvalidOperationException("Worker startup dependency check failed: database is not reachable.");

        await _redis.GetDatabase().PingAsync();

        using var connection = RabbitMqHealthCheck.CreateConnection(_rabbitMqOptions);
        using var channel = connection.CreateModel();

        _logger.LogInformation("Worker dependency validation succeeded for database, Redis, and RabbitMQ.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
