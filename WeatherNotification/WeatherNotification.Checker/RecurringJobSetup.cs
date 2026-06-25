using Hangfire;
using WeatherNotification.Checker.Jobs;

namespace WeatherNotification.Checker;

internal sealed class RecurringJobSetup : IHostedService
{
    private readonly IRecurringJobManager _manager;

    public RecurringJobSetup(IRecurringJobManager manager) => _manager = manager;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // "0 8 * * *" = every day at 08:00
        _manager.AddOrUpdate<WeatherCheckSchedulerJob>(
            "daily-weather-check",
            job => job.ExecuteAsync(),
            "0 8 * * *");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
