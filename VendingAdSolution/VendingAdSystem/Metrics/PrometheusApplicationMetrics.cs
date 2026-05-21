using Prometheus;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Metrics;

public sealed class PrometheusApplicationMetrics : IApplicationMetrics
{
    private static readonly Counter CacheOperations = Prometheus.Metrics.CreateCounter(
        "vendingad_cache_operations_total",
        "Total cache operations grouped by area and result.",
        new CounterConfiguration
        {
            LabelNames = new[] { "area", "result" }
        });

    private static readonly Histogram DatabaseQueryDuration = Prometheus.Metrics.CreateHistogram(
        "vendingad_database_query_duration_seconds",
        "Database query duration in seconds grouped by operation.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "operation" },
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 10)
        });

    private static readonly Gauge ActiveDevices = Prometheus.Metrics.CreateGauge(
        "vendingad_active_devices",
        "Number of active devices configured in the system.");

    public void RecordCacheHit(string area)
    {
        CacheOperations.WithLabels(Normalize(area), "hit").Inc();
    }

    public void RecordCacheMiss(string area)
    {
        CacheOperations.WithLabels(Normalize(area), "miss").Inc();
    }

    public IDisposable ObserveDatabaseQuery(string operation)
    {
        return DatabaseQueryDuration.WithLabels(Normalize(operation)).NewTimer();
    }

    public void SetActiveDevices(int count)
    {
        ActiveDevices.Set(Math.Max(0, count));
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Trim().ToLowerInvariant();
    }
}
