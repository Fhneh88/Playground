using Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Consumer;

public class TemperatureConsumer(ProcessedMessages processed, ILogger<TemperatureConsumer> logger)
    : IConsumer<TemperatureReading>
{
    public Task Consume(ConsumeContext<TemperatureReading> context)
    {
        var msg = context.Message;

        if (!processed.TryAdd(msg.MessageId))
        {
            logger.LogDebug("Duplicate message {MessageId} ignored", msg.MessageId);
            return Task.CompletedTask;
        }

        var timestamp = msg.MeasuredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] {msg.City}: {msg.TemperatureC} C");

        if (msg.TemperatureC < 0)
            logger.LogWarning("Sub-zero temperature in {City}: {Temp}°C", msg.City, msg.TemperatureC);

        return Task.CompletedTask;
    }
}
