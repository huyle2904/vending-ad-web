using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.Services;
using VendingAdSystem.Infrastructure.Persistence;

namespace VendingAdSystem.Metrics;

public sealed class ActiveDeviceMetricsCollector : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IApplicationMetrics _metrics;
    private readonly ILogger<ActiveDeviceMetricsCollector> _logger;

    public ActiveDeviceMetricsCollector(
        IServiceScopeFactory scopeFactory,
        IApplicationMetrics metrics,
        ILogger<ActiveDeviceMetricsCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeDevices = await db.Devices
                .AsNoTracking()
                .CountAsync(device => device.IsActive, cancellationToken);

            _metrics.SetActiveDevices(activeDevices);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh active device metrics.");
        }
    }
}
