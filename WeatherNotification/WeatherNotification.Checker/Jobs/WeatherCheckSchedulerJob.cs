using Hangfire;
using Microsoft.EntityFrameworkCore;
using WeatherNotification.Data;

namespace WeatherNotification.Checker.Jobs;

public class WeatherCheckSchedulerJob
{
    private readonly SubscriptionsDbContext _db;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<WeatherCheckSchedulerJob> _logger;

    public WeatherCheckSchedulerJob(
        SubscriptionsDbContext db,
        IBackgroundJobClient jobClient,
        ILogger<WeatherCheckSchedulerJob> logger)
    {
        _db = db;
        _jobClient = jobClient;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Weather check scheduler started");

        var ids = await _db.Subscriptions.Select(s => s.Id).ToListAsync();
        _logger.LogInformation("Found {Count} active subscriptions", ids.Count);

        var batches = ids.Chunk(10).ToList();
        foreach (var batch in batches)
        {
            var batchList = batch.ToList();
            _jobClient.Enqueue<WeatherBatchProcessorJob>(j => j.ProcessBatchAsync(batchList));
            _logger.LogInformation("Enqueued batch of {Count} subscriptions", batchList.Count);
        }

        _logger.LogInformation("Scheduled {Batches} batch jobs for {Total} subscriptions",
            batches.Count, ids.Count);
    }
}
