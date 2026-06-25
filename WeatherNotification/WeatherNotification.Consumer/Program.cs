using MassTransit;
using Serilog;
using StackExchange.Redis;
using WeatherNotification.Consumer.Consumers;

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

    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<ExtremeWeatherConsumer>();

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(
                builder.Configuration["RabbitMq:Host"],
                builder.Configuration["RabbitMq:VirtualHost"],
                h =>
                {
                    h.Username(builder.Configuration["RabbitMq:Username"]!);
                    h.Password(builder.Configuration["RabbitMq:Password"]!);
                });

            cfg.ConfigureEndpoints(ctx);
        });
    });

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Consumer terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
