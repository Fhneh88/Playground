План: WeatherNotification Service
Структура solution

WeatherNotification/
├── WeatherNotification.sln
├── WeatherNotification.Contracts/     # shared: сообщения + модели БД
├── WeatherNotification.Data/          # EF Core: Subscription + DbContext
├── WeatherNotification.Api/           # ASP.NET Core: POST/DELETE /subscribe
├── WeatherNotification.Checker/       # Worker: Hangfire + OpenMeteo + RabbitMQ producer
└── WeatherNotification.Consumer/      # Worker: MassTransit consumer + Redis
Почему отдельный Data-проект: и Api, и Checker читают/пишут подписки - дублировать DbContext нежелательно.

Шаг 1 - WeatherNotification.Contracts
Один файл, единственная цель:


// ExtremeWeatherDetected.cs
namespace WeatherNotification.Contracts;

public record ExtremeWeatherDetected(Guid SubscriptionId, string City, double TemperatureC);
Зависимости: нет.

Шаг 2 - WeatherNotification.Data
Пакеты: Microsoft.EntityFrameworkCore


Models/Subscription.cs        → Id, City, Email, CreatedAt
SubscriptionsDbContext.cs      → DbSet<Subscription>, OnModelCreating
Ни Npgsql, ни провайдер здесь не нужен - провайдер подключается в Program.cs каждого сервиса через UseNpgsql(...).

Шаг 3 - WeatherNotification.Api
Пакеты: Npgsql.EntityFrameworkCore.PostgreSQL, Serilog.AspNetCore, Serilog.Sinks.Console

Ссылки: → Contracts, → Data


Program.cs
  UseSerilog(...)
  AddDbContext<SubscriptionsDbContext>(UseNpgsql)
  db.Database.EnsureCreatedAsync()   // в prod - MigrateAsync
  MapSubscriptionEndpoints()

Endpoints/SubscriptionEndpoints.cs
  POST /subscribe  { city, email } → 201 Created + { id }
  DELETE /subscribe/{id}           → 204 / 404
Логируем: подписку создали / удалили, 404 если не найдено.

Шаг 4 - WeatherNotification.Checker
Пакеты: Hangfire.AspNetCore, Hangfire.PostgreSql, MassTransit.RabbitMQ, Microsoft.Extensions.Http.Resilience, Npgsql.EntityFrameworkCore.PostgreSQL, Serilog.Extensions.Hosting, Serilog.Sinks.Console

Ссылки: → Contracts, → Data

4a. Weather-клиент

Weather/
  IWeatherProvider.cs              → Task<double> GetTemperatureAsync(string city)
  WeatherProviderException.cs      → : Exception
  Models/GeocodingResponse.cs      → { Results[{ Latitude, Longitude }] }
  Models/ForecastResponse.cs       → { CurrentWeather.Temperature }
  OpenMeteoClient.cs               → : IWeatherProvider
OpenMeteoClient использует два named-клиента:

Имя	BaseAddress
OpenMeteoForecast	https://api.open-meteo.com/
OpenMeteoGeocoding	https://geocoding-api.open-meteo.com/
Оба регистрируются с .AddStandardResilienceHandler(). Логика:

Geocoding: GET v1/search?name={city}&count=1 → lat/lon
Forecast: GET v1/forecast?latitude=…&longitude=…&current_weather=true → temperature
Любая ошибка → throw new WeatherProviderException(...)
4b. Hangfire-jobs

Jobs/
  WeatherCheckSchedulerJob.cs    → recurring, 8:00
  WeatherBatchProcessorJob.cs    → fire-and-forget per batch
Scheduler (recurring "0 8 * * *"):


1. SELECT Id FROM Subscriptions         → Log: "Found N subscriptions"
2. .Chunk(10)
3. foreach batch → IBackgroundJobClient.Enqueue<WeatherBatchProcessorJob>(
       j => j.ProcessBatchAsync(batchIds))  → Log: "Enqueued batch of K"
BatchProcessor (fire-and-forget):


1. Load subscriptions by IDs from DB
2. foreach sub:
     temp = IWeatherProvider.GetTemperatureAsync(city)    → Log temp
     if temp < -10 || temp > 35:
         IPublishEndpoint.Publish(ExtremeWeatherDetected)  → Log: "Published event"
     catch WeatherProviderException → Log.Error (не роняем батч)
4c. Program.cs

UseSerilog(...)
AddDbContext<SubscriptionsDbContext>(UseNpgsql)
AddHttpClient("OpenMeteoForecast", ...).AddStandardResilienceHandler()
AddHttpClient("OpenMeteoGeocoding", ...).AddStandardResilienceHandler()
AddTransient<IWeatherProvider, OpenMeteoClient>()
AddHangfire(cfg => cfg.UsePostgreSqlStorage(...))
AddHangfireServer()
AddMassTransit(x => x.UsingRabbitMq(...))   // только producer, без consumers
AddScoped<WeatherCheckSchedulerJob>()
AddScoped<WeatherBatchProcessorJob>()
AddHostedService<RecurringJobSetup>()        // регистрирует recurring job при старте
Шаг 5 - WeatherNotification.Consumer
Пакеты: MassTransit.RabbitMQ, Serilog.Extensions.Hosting, Serilog.Sinks.Console, StackExchange.Redis

Ссылки: → Contracts

5a. ExtremeWeatherConsumer

Consumers/ExtremeWeatherConsumer.cs   → IConsumer<ExtremeWeatherDetected>
Логика идемпотентности:


redisKey = $"weather:processed:{context.MessageId}"
if await redis.KeyExistsAsync(redisKey) → Log.Warning "Already processed" → return
--- обработка ---
Console.WriteLine($"[NOTIFICATION] {city}: {temp}°C")
Log.Information "Sent notification"
await redis.StringSetAsync(redisKey, "1", TTL: 24h)
5b. Program.cs

UseSerilog(...)
AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnStr))
AddMassTransit(x => {
    x.AddConsumer<ExtremeWeatherConsumer>()
    x.UsingRabbitMq((ctx, cfg) => {
        cfg.Host(...)
        cfg.ConfigureEndpoints(ctx)   // auto-создаёт очередь
    })
})
Шаг 6 - Инфраструктура
docker-compose.yml с тремя сервисами:

Сервис	Image	Порты
postgres	postgres:16-alpine	5432
rabbitmq	rabbitmq:3-management-alpine	5672, 15672
redis	redis:7-alpine	6379
Граф зависимостей между проектами

Contracts  ←──────────────────────────────┐
Data       ← Contracts                     │
Api        ← Data, Contracts               │
Checker    ← Data, Contracts               │
Consumer   ←──────────────────────────────┘
Порядок реализации
#	Что	Проверка
1	docker-compose up	postgres/rabbit/redis доступны
2	Contracts + Data + миграции	dotnet build
3	Api	POST /subscribe → строка в БД
4	OpenMeteoClient	unit-тест с mock HttpClient
5	Hangfire jobs	запуск вручную через Hangfire Dashboard
6	Consumer	сообщение в RabbitMQ → вывод в консоль
7	Serilog везде	структурированные логи с {City}, {Temp}, {SubscriptionId}
