using Microsoft.EntityFrameworkCore;
using Serilog;
using WeatherNotification.Api.Endpoints;
using WeatherNotification.Data;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddDbContext<SubscriptionsDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

    var app = builder.Build();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SubscriptionsDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database schema ensured");
    }

    app.MapSubscriptionEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;
        // Use Append to avoid ArgumentException when header already exists
        context.Response.Headers.Append("X-Correlation-ID", correlationId);

        await _next(context);
    }
}
