using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WeatherNotification.Checker;
using WeatherNotification.Checker.Jobs;
using WeatherNotification.Checker.Weather;
using WeatherNotification.Data;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, config) => config
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddDbContext<SubscriptionsDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

    builder.Services.AddHttpClient("OpenMeteoForecast", c =>
        c.BaseAddress = new Uri("https://api.open-meteo.com/"))
        .AddStandardResilienceHandler();

    builder.Services.AddHttpClient("OpenMeteoGeocoding", c =>
        c.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/"))
        .AddStandardResilienceHandler();

    builder.Services.AddTransient<IWeatherProvider, OpenMeteoClient>();

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o =>
            o.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));

    builder.Services.AddHangfireServer();

    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((_, cfg) =>
        {
            cfg.Host(
                builder.Configuration["RabbitMq:Host"],
                builder.Configuration["RabbitMq:VirtualHost"],
                h =>
                {
                    h.Username(builder.Configuration["RabbitMq:Username"]!);
                    h.Password(builder.Configuration["RabbitMq:Password"]!);
                });
        });
    });

    builder.Services.AddScoped<WeatherCheckSchedulerJob>();
    builder.Services.AddScoped<WeatherBatchProcessorJob>();
    builder.Services.AddHostedService<RecurringJobSetup>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Checker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
