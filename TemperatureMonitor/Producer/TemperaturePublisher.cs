using Contracts;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Producer;

public class TemperaturePublisher(IPublishEndpoint publishEndpoint, ILogger<TemperaturePublisher> logger)
    : BackgroundService
{
    private static readonly string[] Cities = ["Moscow", "London", "Berlin", "Paris", "Oslo"];
    private static readonly Random Rng = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var city = Cities[Rng.Next(Cities.Length)];
            var temp = Math.Round(Rng.NextDouble() * 60 - 20, 1);
            var reading = new TemperatureReading(Guid.NewGuid(), city, temp, DateTime.UtcNow);

            await publishEndpoint.Publish(reading, stoppingToken);
            logger.LogInformation("Published: {City} {Temp}°C", reading.City, reading.TemperatureC);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
