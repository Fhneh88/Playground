# WeatherNotification - Руководство по работе с проектом

## Требования

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Быстрый старт

### 1. Поднять инфраструктуру

```bash
docker compose up -d
```

Это запустит:
- **PostgreSQL** на порту `5432` (база `weatherdb`)
- **RabbitMQ** на порту `5672` (UI: http://localhost:15672, guest/guest)
- **Redis** на порту `6379`

Проверить статус:
```bash
docker compose ps
```

### 2. Запустить сервисы

Каждый сервис запускается в отдельном терминале из папки проекта:

```bash
# Терминал 1 - API
dotnet run --project WeatherNotification.Api

# Терминал 2 - Checker (Hangfire Worker)
dotnet run --project WeatherNotification.Checker

# Терминал 3 - Consumer (уведомления)
dotnet run --project WeatherNotification.Consumer
```

---

## Работа с API

### Подписаться на уведомления

```http
POST http://localhost:5000/subscribe
Content-Type: application/json

{
  "city": "Moscow",
  "email": "user@example.com"
}
```

**Ответ `201 Created`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "city": "Moscow",
  "email": "user@example.com"
}
```

Сохраните `id` - он понадобится для отписки.

### Отписаться

```http
DELETE http://localhost:5000/subscribe/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Ответ:** `204 No Content` (успешно) или `404 Not Found` (не найдено).

### Примеры через curl

```bash
# Подписаться
curl -X POST http://localhost:5000/subscribe \
  -H "Content-Type: application/json" \
  -d '{"city": "Moscow", "email": "user@example.com"}'

# Отписаться (подставьте свой id)
curl -X DELETE http://localhost:5000/subscribe/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

---

## Ручная проверка работы Checker

По умолчанию job запускается в 8:00. Для немедленного запуска используйте **Hangfire Dashboard**.

### Открыть Hangfire Dashboard

Перейдите в браузере по адресу:
```
http://localhost:5000/hangfire
```

> Порт может отличаться - посмотрите вывод `dotnet run --project WeatherNotification.Checker`.

### Запустить job вручную

1. Перейдите в раздел **Recurring Jobs**
2. Найдите задание `daily-weather-check`
3. Нажмите кнопку **Trigger now**

После запуска:
- В разделе **Succeeded** появятся выполненные батч-задания
- В консоли Consumer появятся уведомления для городов с экстремальной температурой

---

## Что видно в консолях

### WeatherNotification.Api
```
[INF] Database schema ensured
[INF] New subscription created: Id=..., City=Moscow, Email=user@example.com
```

### WeatherNotification.Checker
```
[INF] Weather check scheduler started
[INF] Found 3 active subscriptions
[INF] Enqueued batch of 3 subscriptions
[INF] Resolving coordinates for city Moscow
[INF] City Moscow → lat=55.75, lon=37.62
[INF] Temperature in Moscow: -15.2°C
[WRN] Extreme temperature -15.2°C in Moscow for subscription ... - publishing event
```

### WeatherNotification.Consumer
```
[INF] Processing ExtremeWeatherDetected: City=Moscow, Temp=-15.2°C, ...
[NOTIFICATION] Extreme weather in Moscow: -15.2°C (subscription: ...)
[INF] Message ... marked as processed in Redis (TTL=24h)
```

---

## Проверка идемпотентности Consumer

Если то же самое сообщение будет доставлено повторно (например, при сбое), Consumer обнаружит его в Redis и пропустит:

```
[WRN] Message 3fa85f64-... already processed - skipping (idempotency check)
```

Для теста вручную:
1. Опубликуйте сообщение в RabbitMQ UI (`http://localhost:15672`)
2. Очередь: `WeatherNotification.Consumer:ExtremeWeatherDetected`
3. Опубликуйте то же сообщение ещё раз с тем же `messageId`
4. Во второй раз увидите `already processed` в логе Consumer

---

## Подключение к базе данных

Проверить подписки напрямую в PostgreSQL:

```bash
docker exec -it weathernotification-postgres-1 psql -U postgres -d weatherdb
```

```sql
SELECT * FROM "Subscriptions";
```

---

## Конфигурация

Все connection strings находятся в `appsettings.json` каждого сервиса.

| Параметр | Файл | Значение по умолчанию |
|---|---|---|
| PostgreSQL | `Api/appsettings.json`, `Checker/appsettings.json` | `Host=localhost;Port=5432;Database=weatherdb;Username=postgres;Password=postgres` |
| RabbitMQ Host | `Checker/appsettings.json`, `Consumer/appsettings.json` | `localhost` |
| RabbitMQ User | то же | `guest / guest` |
| Redis | `Consumer/appsettings.json` | `localhost:6379` |

Для изменения без правки файлов используйте переменные окружения (ASP.NET Core подхватывает их автоматически):

```bash
# Пример: переопределить строку подключения к Postgres
export ConnectionStrings__Postgres="Host=myserver;..."
```

---

## Остановка

```bash
# Остановить инфраструктуру (данные сохранятся в volume)
docker compose stop

# Остановить и удалить контейнеры + данные
docker compose down -v
```

---

## Структура проекта

```
WeatherNotification/
├── docker-compose.yml
├── PLAN.md                              ← план реализации
├── GUIDE.md                             ← этот файл
├── WeatherNotification.Contracts/
│   └── ExtremeWeatherDetected.cs
├── WeatherNotification.Data/
│   ├── Models/Subscription.cs
│   └── SubscriptionsDbContext.cs
├── WeatherNotification.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   └── Endpoints/SubscriptionEndpoints.cs
├── WeatherNotification.Checker/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── RecurringJobSetup.cs
│   ├── Weather/
│   │   ├── IWeatherProvider.cs
│   │   ├── WeatherProviderException.cs
│   │   ├── OpenMeteoClient.cs
│   │   └── Models/
│   │       ├── GeocodingResponse.cs
│   │       └── ForecastResponse.cs
│   └── Jobs/
│       ├── WeatherCheckSchedulerJob.cs
│       └── WeatherBatchProcessorJob.cs
└── WeatherNotification.Consumer/
    ├── Program.cs
    ├── appsettings.json
    └── Consumers/
        └── ExtremeWeatherConsumer.cs
```
