# WeatherNotification - План реализации

## Постановка задачи

Пользователь подписывается на уведомления о погоде в своём городе. Каждое утро в 8:00 система проверяет погоду через внешний API и отправляет уведомление, если температура вышла за допустимые пределы (ниже −10 °C или выше +35 °C).

---

## Архитектура

```
┌─────────────────┐       PostgreSQL        ┌──────────────────────┐
│  WeatherApi     │ ◄────────────────────── │  WeatherChecker      │
│  (REST API)     │  читает подписки        │  (Hangfire Worker)   │
│  POST /subscribe│                         │  recurring job 8:00  │
│  DELETE /{id}   │                         │  → batch jobs        │
└────────┬────────┘                         └──────────┬───────────┘
         │ хранит подписки                             │ публикует
         ▼                                             ▼
     PostgreSQL                                    RabbitMQ
                                                       │
                                          ┌────────────▼───────────┐
                                          │  NotificationService   │
                                          │  (MassTransit Consumer)│
                                          │  пишет в консоль       │
                                          │  идемпотентность Redis │
                                          └────────────────────────┘
```

### Сервисы

| Сервис | Тип | Назначение |
|---|---|---|
| `WeatherNotification.Api` | ASP.NET Core Web API | Управление подписками |
| `WeatherNotification.Checker` | Worker Service | Проверка погоды + публикация событий |
| `WeatherNotification.Consumer` | Worker Service | Приём событий + отправка уведомлений |

### Вспомогательные проекты

| Проект | Назначение |
|---|---|
| `WeatherNotification.Contracts` | Контракты сообщений RabbitMQ |
| `WeatherNotification.Data` | EF Core модели и DbContext |

---

## Граф зависимостей

```
Contracts  ◄── Api
           ◄── Checker
           ◄── Consumer

Data       ◄── Api
           ◄── Checker
```

---

## Инфраструктура

| Сервис | Образ | Порты |
|---|---|---|
| PostgreSQL | `postgres:16-alpine` | 5432 |
| RabbitMQ | `rabbitmq:3-management-alpine` | 5672, 15672 (UI) |
| Redis | `redis:7-alpine` | 6379 |

---

## Шаг 1 - WeatherNotification.Contracts

**Цель:** единственное место определения контракта сообщения RabbitMQ.

```
WeatherNotification.Contracts/
└── ExtremeWeatherDetected.cs
```

```csharp
public record ExtremeWeatherDetected(Guid SubscriptionId, string City, double TemperatureC);
```

**Пакеты:** нет.

---

## Шаг 2 - WeatherNotification.Data

**Цель:** разделяемый EF Core слой между Api и Checker.

```
WeatherNotification.Data/
├── Models/
│   └── Subscription.cs          ← Id, City, Email, CreatedAt
└── SubscriptionsDbContext.cs    ← DbSet<Subscription>, конфигурация модели
```

**Пакеты:**
- `Microsoft.EntityFrameworkCore`

**Модель:**

```csharp
public class Subscription
{
    public Guid Id { get; set; }
    public required string City { get; set; }   // макс. 100 символов
    public required string Email { get; set; }  // макс. 200 символов
    public DateTime CreatedAt { get; set; }
}
```

---

## Шаг 3 - WeatherNotification.Api

**Цель:** REST API для управления подписками.

```
WeatherNotification.Api/
├── Program.cs
├── appsettings.json
└── Endpoints/
    └── SubscriptionEndpoints.cs
```

### Эндпоинты

| Метод | Маршрут | Тело | Ответ |
|---|---|---|---|
| `POST` | `/subscribe` | `{ "city": "Moscow", "email": "a@b.com" }` | `201 Created` + `{ id, city, email }` |
| `DELETE` | `/subscribe/{id}` | - | `204 No Content` / `404 Not Found` |

### Зависимости

```
Program.cs:
  UseSerilog(...)
  AddDbContext<SubscriptionsDbContext>(UseNpgsql)
  db.Database.EnsureCreatedAsync()   ← создаёт таблицы при старте
  MapSubscriptionEndpoints()
```

**Пакеты:**
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Microsoft.EntityFrameworkCore.Design`
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`

---

## Шаг 4 - WeatherNotification.Checker

**Цель:** ежедневно в 8:00 проверять погоду для всех подписок и публиковать события об экстремальных температурах.

```
WeatherNotification.Checker/
├── Program.cs
├── appsettings.json
├── RecurringJobSetup.cs                   ← IHostedService, регистрирует cron-job
├── Weather/
│   ├── IWeatherProvider.cs
│   ├── WeatherProviderException.cs
│   ├── OpenMeteoClient.cs
│   └── Models/
│       ├── GeocodingResponse.cs
│       └── ForecastResponse.cs
└── Jobs/
    ├── WeatherCheckSchedulerJob.cs        ← recurring job
    └── WeatherBatchProcessorJob.cs        ← fire-and-forget job
```

### Weather Client

`OpenMeteoClient` реализует `IWeatherProvider`:

```
1. GET geocoding-api.open-meteo.com/v1/search?name={city}
   → получаем latitude, longitude

2. GET api.open-meteo.com/v1/forecast?latitude=…&longitude=…&current_weather=true
   → получаем temperature
```

Оба HTTP-клиента зарегистрированы с `AddStandardResilienceHandler()` (retry + circuit breaker).  
Любая ошибка API → `throw new WeatherProviderException(...)`.

### Hangfire Jobs

**WeatherCheckSchedulerJob** (cron: `"0 8 * * *"`):
```
1. Загрузить все Id подписок из БД
2. .Chunk(10) → батчи по 10
3. Для каждого батча → IBackgroundJobClient.Enqueue<WeatherBatchProcessorJob>
```

**WeatherBatchProcessorJob** (fire-and-forget):
```
1. Загрузить подписки по Id
2. Для каждой:
   a. GetTemperatureAsync(city)
   b. Если temp < -10 || temp > 35:
      → IPublishEndpoint.Publish(ExtremeWeatherDetected)
   c. WeatherProviderException → Log.Error, продолжить батч
```

### Регистрация recurring job

`RecurringJobSetup : IHostedService` вызывает `IRecurringJobManager.AddOrUpdate` при старте хоста, что гарантирует наличие задания в Hangfire storage.

**Пакеты:**
- `Hangfire.AspNetCore`
- `Hangfire.PostgreSql`
- `MassTransit.RabbitMQ`
- `Microsoft.Extensions.Http.Resilience`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Serilog.Extensions.Hosting`
- `Serilog.Settings.Configuration`
- `Serilog.Sinks.Console`

---

## Шаг 5 - WeatherNotification.Consumer

**Цель:** принимать события `ExtremeWeatherDetected` и отправлять уведомления (вывод в консоль). Consumer идемпотентен.

```
WeatherNotification.Consumer/
├── Program.cs
├── appsettings.json
└── Consumers/
    └── ExtremeWeatherConsumer.cs
```

### Идемпотентность

```
redisKey = "weather:processed:{MessageId}"

if Redis.KeyExists(redisKey):
    Log.Warning "Already processed" → return

// обработка сообщения
Console.WriteLine("[NOTIFICATION] ...")

Redis.StringSet(redisKey, "1", TTL: 24 часа)
```

**Пакеты:**
- `MassTransit.RabbitMQ`
- `StackExchange.Redis`
- `Serilog.Extensions.Hosting`
- `Serilog.Settings.Configuration`
- `Serilog.Sinks.Console`

---

## Шаг 6 - Логирование

Serilog настроен во всех трёх сервисах через `appsettings.json`. Каждый ключевой этап логируется:

| Событие | Уровень | Поля |
|---|---|---|
| Подписка создана / удалена | `Information` | `{Id}`, `{City}`, `{Email}` |
| Найдены подписки для проверки | `Information` | `{Count}` |
| Батч поставлен в очередь | `Information` | `{Count}` |
| Координаты города получены | `Information` | `{City}`, `{Lat}`, `{Lon}` |
| Температура получена | `Information` | `{City}`, `{Temp}` |
| Экстремальная температура | `Warning` | `{Temp}`, `{City}`, `{Id}` |
| Событие опубликовано | *(в Warning выше)* | - |
| Ошибка получения погоды | `Error` | `{City}`, `{Id}` |
| Сообщение уже обработано | `Warning` | `{MessageId}` |
| Уведомление отправлено | `Information` | `{City}`, `{Temp}`, `{SubscriptionId}` |

---

## Порядок реализации и проверки

| # | Что делаем | Как проверяем |
|---|---|---|
| 1 | `docker compose up -d` | postgres, rabbit, redis доступны |
| 2 | Contracts + Data | `dotnet build` |
| 3 | Api | `POST /subscribe` → строка в таблице `Subscriptions` |
| 4 | OpenMeteoClient | unit-тест с mock-HttpClient или ручной запрос к Open-Meteo |
| 5 | Checker (Hangfire) | Hangfire Dashboard → ручной запуск job |
| 6 | Consumer | сообщение в RabbitMQ → строка в консоли Consumer |
| 7 | Serilog | структурированные логи с именованными полями |
| 8 | Идемпотентность | повторная доставка того же MessageId → `Already processed` в логе |
