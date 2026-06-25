using Contracts;
using Consumer;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace TemperatureMonitor.Tests;

/// <summary>
/// Тесты консюмера: форматирование вывода, идемпотентность, логирование предупреждений.
/// </summary>
public class TemperatureConsumerTests
{
    // --- Вспомогательные фабрики ---

    private static ConsumeContext<TemperatureReading> MakeContext(TemperatureReading msg)
    {
        var ctx = Substitute.For<ConsumeContext<TemperatureReading>>();
        ctx.Message.Returns(msg);
        return ctx;
    }

    private static TemperatureReading MakeReading(double temp = 5.0, string city = "Moscow")
        => new(Guid.NewGuid(), city, temp, new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc));

    // --- Форматирование вывода ---

    [Fact]
    public async Task Consume_NewMessage_WritesFormattedLineToConsole()
    {
        var logger   = new CapturingLogger<TemperatureConsumer>();
        var consumer = new TemperatureConsumer(new ProcessedMessages(), logger);
        var msg      = MakeReading(city: "Berlin", temp: -3.5);

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            await consumer.Consume(MakeContext(msg));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();
        Assert.Contains("Berlin",  output);
        Assert.Contains("-3.5 C",  output);
        // формат даты [yyyy-MM-dd HH:mm:ss]
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]", output);
    }

    // --- Идемпотентность ---

    [Fact]
    public async Task Consume_DuplicateMessage_LogsDebugAndDoesNotPrintTwice()
    {
        var logger   = new CapturingLogger<TemperatureConsumer>();
        var consumer = new TemperatureConsumer(new ProcessedMessages(), logger);
        var ctx      = MakeContext(MakeReading());

        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            await consumer.Consume(ctx);
            await consumer.Consume(ctx); // повтор с тем же MessageId
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // строка в консоль пишется ровно один раз
        Assert.Single(sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        // дублирующее сообщение логируется как Debug
        Assert.Single(logger.Debugs);
        Assert.Contains("Duplicate", logger.Debugs.Single().Message);
    }

    // --- Логирование предупреждений ---

    [Fact]
    public async Task Consume_SubZeroTemperature_LogsWarning()
    {
        var logger   = new CapturingLogger<TemperatureConsumer>();
        var consumer = new TemperatureConsumer(new ProcessedMessages(), logger);

        await consumer.Consume(MakeContext(MakeReading(temp: -5.0, city: "Oslo")));

        Assert.Single(logger.Warnings);
        Assert.Contains("Oslo", logger.Warnings.Single().Message);
    }

    [Fact]
    public async Task Consume_AboveZeroTemperature_NoWarning()
    {
        var logger   = new CapturingLogger<TemperatureConsumer>();
        var consumer = new TemperatureConsumer(new ProcessedMessages(), logger);

        await consumer.Consume(MakeContext(MakeReading(temp: 20.0)));

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task Consume_ExactlyZeroTemperature_NoWarning()
    {
        // граница: условие строгое (< 0), ноль не должен давать предупреждение
        var logger   = new CapturingLogger<TemperatureConsumer>();
        var consumer = new TemperatureConsumer(new ProcessedMessages(), logger);

        await consumer.Consume(MakeContext(MakeReading(temp: 0.0)));

        Assert.Empty(logger.Warnings);
    }
}
