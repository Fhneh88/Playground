using Contracts;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Producer;

namespace TemperatureMonitor.Tests;

/// <summary>
/// Тесты фонового сервиса-продюсера: публикация, валидность полей, остановка по cancellation.
/// </summary>
public class TemperaturePublisherTests
{
    private static readonly string[] KnownCities = ["Moscow", "London", "Berlin", "Paris", "Oslo"];

    // Хелпер: запускает Publisher, ждёт первой публикации (mock отменяет CTS),
    // затем ждёт завершения фонового таска.
    private static async Task<TemperatureReading?> RunOneIterationAsync(
        IPublishEndpoint endpoint, CancellationTokenSource cts)
    {
        var publisher = new TemperaturePublisher(endpoint, NullLogger<TemperaturePublisher>.Instance);
        await publisher.StartAsync(cts.Token);
        // Даём фоновому таску время завершиться после отмены
        await Task.Delay(300);
        return null;
    }

    // --- Факт публикации ---

    [Fact]
    public async Task ExecuteAsync_OnFirstIteration_PublishesExactlyOneMessage()
    {
        var cts      = new CancellationTokenSource();
        var endpoint = Substitute.For<IPublishEndpoint>();

        endpoint.Publish(Arg.Any<TemperatureReading>(), Arg.Any<CancellationToken>())
            .Returns(_ => { cts.Cancel(); return Task.CompletedTask; });

        var publisher = new TemperaturePublisher(endpoint, NullLogger<TemperaturePublisher>.Instance);
        await publisher.StartAsync(cts.Token);
        await Task.Delay(300);

        await endpoint.Received(1)
            .Publish(Arg.Any<TemperatureReading>(), Arg.Any<CancellationToken>());
    }

    // --- Валидность полей опубликованного сообщения ---

    [Fact]
    public async Task ExecuteAsync_PublishedMessage_HasValidFields()
    {
        var cts      = new CancellationTokenSource();
        TemperatureReading? captured = null;
        var endpoint = Substitute.For<IPublishEndpoint>();
        var before   = DateTime.UtcNow;

        endpoint.Publish(Arg.Any<TemperatureReading>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.Arg<TemperatureReading>();
                cts.Cancel();
                return Task.CompletedTask;
            });

        var publisher = new TemperaturePublisher(endpoint, NullLogger<TemperaturePublisher>.Instance);
        await publisher.StartAsync(cts.Token);
        await Task.Delay(300);
        var after = DateTime.UtcNow;

        Assert.NotNull(captured);
        Assert.NotEqual(Guid.Empty, captured.MessageId);
        Assert.Contains(captured.City, KnownCities);
        // температура генерируется формулой: Random * 60 - 20 → [-20, 40]
        Assert.InRange(captured.TemperatureC, -20.0, 40.0);
        Assert.InRange(captured.MeasuredAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    // --- Остановка по отмене ---

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_DoesNotPublishMoreThanOnce()
    {
        var cts      = new CancellationTokenSource();
        int count    = 0;
        var endpoint = Substitute.For<IPublishEndpoint>();

        endpoint.Publish(Arg.Any<TemperatureReading>(), Arg.Any<CancellationToken>())
            .Returns(_ => { count++; cts.Cancel(); return Task.CompletedTask; });

        var publisher = new TemperaturePublisher(endpoint, NullLogger<TemperaturePublisher>.Instance);
        await publisher.StartAsync(cts.Token);
        await Task.Delay(300);

        Assert.Equal(1, count);
    }
}
