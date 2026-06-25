# TaskTracker

REST API для управления проектами и задачами. Проекты содержат задачи, задачи можно фильтровать по статусу и приоритету и вешать на них теги.

## Эндпоинты

### Проекты

```text
GET  /api/projects              - список проектов с количеством задач
POST /api/projects              - создать проект
GET  /api/projects/{id}         - проект со всеми задачами и тегами
GET  /api/projects/{id}/stats   - статистика задач: сколько в каждом статусе
```

### Задачи

```text
GET  /api/projects/{id}/tasks              - задачи проекта (фильтры: ?status=&priority=)
POST /api/projects/{id}/tasks              - создать задачу
PUT  /api/tasks/{id}/status                - обновить статус { "status": "InProgress" }
```

### Теги

```text
POST /api/tags                             - создать тег
POST /api/tasks/{taskId}/tags/{tagId}      - привязать тег к задаче
```

Статусы: `Todo`, `InProgress`, `Done`. Приоритеты: `Low`, `Medium`, `High`.

## Что внутри

Два middleware: `RequestLoggingMiddleware` логирует каждый входящий запрос, `ExceptionHandlingMiddleware` перехватывает необработанные исключения и возвращает JSON с ошибкой вместо стектрейса.

OpenAPI-документация подключена в Development-режиме (`/openapi`).

## Запуск

Сначала нужен PostgreSQL - проще всего через Docker:

```bash
docker compose up -d
dotnet run --project TaskTracker
```

Схема БД создаётся автоматически при старте через `EnsureCreated`.
