namespace VendingAdSystem.Application.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default);
}
