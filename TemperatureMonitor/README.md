# TemperatureMonitor

Учебный проект: два консольных приложения, обменивающихся сообщениями через RabbitMQ с помощью MassTransit.

## Структура решения

```
TemperatureMonitor/
├── Contracts/                  # Общие типы сообщений
├── Producer/                   # Публикует события каждые 5 секунд
├── Consumer/                   # Читает события, логирует предупреждения
└── TemperatureMonitor.Tests/   # Unit-тесты (xUnit + NSubstitute)
```

## Как это работает

### Контракт (`Contracts`)

Единственный тип сообщения, разделяемый между приложениями:

```csharp
record TemperatureReading(Guid MessageId, string City, double TemperatureC, DateTime MeasuredAt);
```

`MessageId` - уникальный идентификатор каждого события, используется Consumer для идемпотентности.

---

### Producer

`TemperaturePublisher` - `BackgroundService`, который в бесконечном цикле:

1. Выбирает случайный город из набора (`Moscow`, `London`, `Berlin`, `Paris`, `Oslo`).
2. Генерирует случайную температуру в диапазоне **[-20, 40] °C**.
3. Публикует `TemperatureReading` через `IPublishEndpoint` (MassTransit).
4. Ждёт **5 секунд** и повторяет.

MassTransit автоматически создаёт exchange в RabbitMQ при старте и маршрутизирует сообщения всем подписчикам.

```
Producer → [RabbitMQ exchange: Contracts:TemperatureReading] → очередь Consumer
```

---

### Consumer

При старте регистрирует `TemperatureConsumer` и подписывается на очередь, которую MassTransit создаёт автоматически через `cfg.ConfigureEndpoints(ctx)`.

**`TemperatureConsumer.Consume`** при получении сообщения:

1. **Проверяет `ProcessedMessages`** - если `MessageId` уже видели, логирует `Debug` и завершает обработку без каких-либо действий (идемпотентность).
2. Форматирует и выводит строку в консоль:
   ```
   [2026-04-18 12:00:00] Moscow: -3.5 C
   ```
3. Если `TemperatureC < 0` - дополнительно вызывает `ILogger.LogWarning`.

**`ProcessedMessages`** - синглтон (`AddSingleton`) с `HashSet<Guid>` в памяти. `TryAdd` возвращает `false` для уже обработанных ID - это гарантирует, что повторный запуск Consumer или переподключение после сбоя не приведут к двойной обработке сообщений, **пока процесс не перезапущен**.

---

### Поток сообщения

```
Producer                          RabbitMQ                        Consumer
   │                                  │                               │
   │── Publish(TemperatureReading) ──►│                               │
   │                                  │── deliver to queue ──────────►│
   │                                  │                               │── ProcessedMessages.TryAdd(MessageId)
   │                                  │                               │   ├─ true  → вывод + LogWarning (если < 0)
   │                                  │                               │   └─ false → LogDebug "Duplicate, ignored"
```

---

## Предварительные требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- RabbitMQ, доступный на `localhost:5672` (учётные данные: `guest` / `guest`)

Быстрый запуск RabbitMQ через Docker:

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

После запуска Management UI доступен по адресу `http://localhost:15672` (`guest` / `guest`).

---

## Запуск

В двух отдельных терминалах:

```bash
# Терминал 1 - Consumer (запустить первым, чтобы очередь существовала до первого сообщения)
dotnet run --project Consumer

# Терминал 2 - Producer
dotnet run --project Producer
```

Ожидаемый вывод в консоли Consumer:

```
[2026-04-18 12:00:00] Berlin: 12.3 C
[2026-04-18 12:00:05] Oslo: -5.1 C
warn: Consumer.TemperatureConsumer[0]
      Sub-zero temperature in Oslo: -5.1°C
[2026-04-18 12:00:10] Moscow: 7.8 C
```

---

## Тесты

```bash
dotnet test TemperatureMonitor.Tests
```

| Группа | Тестов | Что проверяется |
|---|:---:|---|
| `ProcessedMessagesTests` | 4 | Первый ID принимается; дубль отклоняется; несколько уникальных ID; корректность после дубля |
| `TemperatureConsumerTests` | 5 | Формат строки вывода; игнорирование дубля + Debug-лог; `LogWarning` при `< 0`; отсутствие предупреждения при `>= 0`; граничный случай `0.0` |
| `TemperaturePublisherTests` | 3 | Публикуется ровно одно сообщение на итерацию; все поля валидны; цикл останавливается по `CancellationToken` |

### Подход к тестированию

- **`CapturingLogger<T>`** - самодостаточная реализация `ILogger<T>`, записывающая вызовы для проверки уровня и текста без внешних зависимостей.
- **NSubstitute** - мок `IPublishEndpoint` перехватывает вызов `Publish` и немедленно отменяет `CancellationTokenSource`, что позволяет тестировать `BackgroundService` без ожидания 5-секундного `Task.Delay`.
- **NSubstitute** - мок `ConsumeContext<TemperatureReading>` позволяет вызывать `Consume` напрямую без поднятия RabbitMQ.

---

## Ограничения

- `ProcessedMessages` хранит обработанные ID **только в памяти**: после перезапуска Consumer повторная доставка сообщений из очереди будет обработана заново. Для production-сценария нужно персистентное хранилище (Redis, БД).
- Конфигурация подключения к RabbitMQ захардкожена. В реальном проекте следует выносить в `appsettings.json` или переменные окружения.
