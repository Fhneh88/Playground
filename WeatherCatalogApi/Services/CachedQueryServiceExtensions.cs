using StackExchange.Redis;
using WeatherCatalogApi.Services;

namespace WeatherCatalogApi;

public static class CachedQueryServiceExtensions
{
    /// <summary>
    /// Registers MemoryCache (L1) + StackExchange Redis (L2) + CachedQueryService.
    /// <paramref name="instanceName"/> must match the InstanceName in AddStackExchangeRedisCache
    /// so that prefix-based invalidation strips it correctly.
    /// </summary>
    public static IServiceCollection AddCachedQueryService(
        this IServiceCollection services,
        string redisConnectionString,
        string instanceName = "")
    {
        services.AddMemoryCache();
        services.AddStackExchangeRedisCache(opt =>
        {
            opt.Configuration = redisConnectionString;
            opt.InstanceName = instanceName;
        });
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton(new CachedQueryServiceOptions { RedisInstanceName = instanceName });
        services.AddSingleton<ICachedQueryService, CachedQueryService>();
        return services;
    }
}
