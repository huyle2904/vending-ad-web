namespace VendingAdSystem.Application.Services;

public interface IApplicationMetrics
{
    void RecordCacheHit(string area);
    void RecordCacheMiss(string area);
    IDisposable ObserveDatabaseQuery(string operation);
    void SetActiveDevices(int count);
}

public sealed class NullApplicationMetrics : IApplicationMetrics
{
    public void RecordCacheHit(string area)
    {
    }

    public void RecordCacheMiss(string area)
    {
    }

    public IDisposable ObserveDatabaseQuery(string operation)
    {
        return NullDisposable.Instance;
    }

    public void SetActiveDevices(int count)
    {
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
