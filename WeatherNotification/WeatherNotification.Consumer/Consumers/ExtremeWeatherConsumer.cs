using MassTransit;
using StackExchange.Redis;
using WeatherNotification.Contracts;

namespace WeatherNotification.Consumer.Consumers;

public class ExtremeWeatherConsumer : IConsumer<ExtremeWeatherDetected>
{
    private readonly IDatabase _redis;
    private readonly ILogger<ExtremeWeatherConsumer> _logger;

    public ExtremeWeatherConsumer(IConnectionMultiplexer redis, ILogger<ExtremeWeatherConsumer> logger)
    {
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExtremeWeatherDetected> context)
    {
        var messageId = context.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var redisKey = $"weather:processed:{messageId}";

        if (await _redis.KeyExistsAsync(redisKey))
        {
            _logger.LogWarning("Message {MessageId} already processed - skipping (idempotency check)", messageId);
            return;
        }

        var msg = context.Message;
        _logger.LogInformation(
            "Processing ExtremeWeatherDetected: City={City}, Temp={Temp}°C, SubscriptionId={SubscriptionId}, MessageId={MessageId}",
            msg.City, msg.TemperatureC, msg.SubscriptionId, messageId);

        Console.WriteLine(
            $"[NOTIFICATION] Extreme weather in {msg.City}: {msg.TemperatureC:F1}°C " +
            $"(subscription: {msg.SubscriptionId})");

        await _redis.StringSetAsync(redisKey, "1", TimeSpan.FromHours(24));
        _logger.LogInformation("Message {MessageId} marked as processed in Redis (TTL=24h)", messageId);
    }
}
