using MassTransit;
using Microsoft.EntityFrameworkCore;
using WeatherNotification.Checker.Weather;
using WeatherNotification.Contracts;
using WeatherNotification.Data;

namespace WeatherNotification.Checker.Jobs;

public class WeatherBatchProcessorJob
{
    private readonly SubscriptionsDbContext _db;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<WeatherBatchProcessorJob> _logger;

    public WeatherBatchProcessorJob(
        SubscriptionsDbContext db,
        IWeatherProvider weatherProvider,
        IPublishEndpoint publishEndpoint,
        ILogger<WeatherBatchProcessorJob> logger)
    {
        _db = db;
        _weatherProvider = weatherProvider;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(List<Guid> subscriptionIds)
    {
        _logger.LogInformation("Processing weather batch: {Count} subscriptions", subscriptionIds.Count);

        var subscriptions = await _db.Subscriptions
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToListAsync();

        foreach (var sub in subscriptions)
        {
            try
            {
                var temp = await _weatherProvider.GetTemperatureAsync(sub.City);

                if (temp < -10 || temp > 35)
                {
                    _logger.LogWarning(
                        "Extreme temperature {Temp}°C in {City} for subscription {Id} - publishing event",
                        temp, sub.City, sub.Id);

                    await _publishEndpoint.Publish(new ExtremeWeatherDetected(sub.Id, sub.City, temp));
                }
                else
                {
                    _logger.LogInformation(
                        "Normal temperature {Temp}°C in {City}, no alert for subscription {Id}",
                        temp, sub.City, sub.Id);
                }
            }
            catch (WeatherProviderException ex)
            {
                _logger.LogError(ex,
                    "Failed to get weather for city {City}, subscription {Id}",
                    sub.City, sub.Id);
            }
        }

        _logger.LogInformation("Batch processing complete for {Count} subscriptions", subscriptionIds.Count);
    }
}
