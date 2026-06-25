namespace WeatherCatalogApi.Services;

public interface ICachedQueryService
{
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan ttl,
        CancellationToken ct = default);

    Task InvalidateAsync(string key, CancellationToken ct = default);
    Task InvalidateByPrefixAsync(string prefix, CancellationToken ct = default);
}
