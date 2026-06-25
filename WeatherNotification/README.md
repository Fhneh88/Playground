# WeatherNotification

Микросервисная система погодных уведомлений. Пользователи подписываются на оповещения, периодический воркер проверяет погоду и публикует уведомления в очередь, консьюмер их обрабатывает.

## Сервисы

### WeatherNotification.Api

HTTP API для управления подписками. Хранит их в PostgreSQL, логирует через Serilog с ротацией по дням.

### WeatherNotification.Checker

Фоновый воркер на **Hangfire**. По расписанию читает активные подписки, запрашивает погоду у [open-meteo.com](https://open-meteo.com) (бесплатный API, без ключа) и публикует уведомления в RabbitMQ через **MassTransit**. Расписание и хранение заданий Hangfire - тоже в PostgreSQL.

### WeatherNotification.Consumer

Принимает сообщения из RabbitMQ и обрабатывает их. Кэширует обработанные уведомления в Redis, чтобы не дублировать их при переподключении.

### Contracts / Data

Общие типы сообщений и Entity Framework контекст, разделяемые между сервисами.

## Поток данных

```text
API          →  PostgreSQL (подписки)
                    ↑
Checker      →  читает подписки
             →  запрашивает open-meteo.com
             →  публикует в RabbitMQ
                    ↓
Consumer     →  получает из RabbitMQ
             →  кэширует в Redis
             →  логирует уведомление
```

## Запуск

```bash
docker compose up --build
```

Это поднимает PostgreSQL, RabbitMQ, Redis и все три сервиса приложения.

- API: `http://localhost:5000`
- RabbitMQ Management UI: `http://localhost:15672` (guest / guest)

Для запуска сервисов по отдельности есть отдельные compose-файлы: `docker-compose.api.yml`, `docker-compose.checker.yml`, `docker-compose.consumer.yml`.
