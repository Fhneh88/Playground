# OrderService - Юнит-тесты с Moq

Учебный проект: реализация сервиса заказов интернет-магазина  
и полное покрытие его юнит-тестами с изоляцией от внешних систем через **Moq**.

---

## Содержание

1. [Структура проекта](#структура-проекта)
2. [Что такое Moq](#что-такое-moq)
3. [Зачем он нужен в этом проекте](#зачем-он-нужен-в-этом-проекте)
4. [Правила написания тестов](#правила-написания-тестов)
5. [Как Moq используется здесь](#как-moq-используется-здесь)
   - [Создание мока и Setup](#создание-мока-и-setup)
   - [It.Is\<T\> - точный матчинг аргументов](#itist--точный-матчинг-аргументов)
   - [Callback - захват аргументов и имитация состояния](#callback--захват-аргументов-и-имитация-состояния)
   - [Verify - проверка количества вызовов](#verify--проверка-количества-вызовов)
   - [Замыкание для имитации кэша (идемпотентность)](#замыкание-для-имитации-кэша-идемпотентность)
6. [Покрытые сценарии](#покрытые-сценарии)
7. [Минусы Moq](#минусы-moq)
8. [Запуск тестов](#запуск-тестов)

---

## Структура проекта

```text
OrderService/
├── OrderService.Core/              # Бизнес-логика (библиотека)
│   ├── Interfaces/                 # Контракты зависимостей
│   │   ├── IOrderRepository.cs
│   │   ├── IProductRepository.cs
│   │   ├── IPaymentGateway.cs
│   │   ├── INotificationService.cs
│   │   └── IIdempotencyStore.cs    ← хранилище для идемпотентных вызовов
│   ├── Models/                     # Доменные модели и DTO
│   │   ├── Order.cs / OrderItem.cs / OrderStatus.cs
│   │   ├── Product.cs
│   │   ├── CreateOrderDto.cs       ← содержит опциональный IdempotencyKey
│   │   ├── CreateOrderResult.cs    ← паттерн Result (Ok / Fail)
│   │   └── PaymentResult.cs
│   └── Services/
│       └── OrderService.cs         # Основная бизнес-логика (7 шагов)
│
└── OrderService.Tests/             # Тесты (xUnit + Moq + FluentAssertions)
    └── OrderServiceTests.cs        # 24 теста в 6 группах
```

---

## Что такое Moq

**Moq** - самая популярная библиотека для создания _моков_ (mock-объектов) в экосистеме .NET.

Мок - это подменный объект, который имитирует поведение реальной зависимости  
(базы данных, платёжного шлюза, почтового сервиса и т. д.) без её реального запуска.

```csharp
// Вместо реального класса, который ходит в БД:
var orderRepo = new Mock<IOrderRepository>();

// Говорим моку: «когда SaveAsync вызовут - верни 99»:
orderRepo
    .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(99);
```

Moq работает через рефлексию и генерирует прокси-классы во время выполнения,  
поэтому мокать можно только **интерфейсы** и **виртуальные методы** классов.

---

## Зачем он нужен в этом проекте

`OrderService.CreateOrderAsync` зависит от пяти внешних систем:

| Зависимость           | Что делает в prod                      | Почему нельзя в тесте                |
| --------------------- | -------------------------------------- | ------------------------------------ |
| `IOrderRepository`    | Пишет заказ в БД                       | Нет БД, медленно, состояние меняется |
| `IProductRepository`  | Читает товары из БД                    | То же самое                          |
| `IPaymentGateway`     | Списывает деньги с карты               | Нельзя делать реальные транзакции    |
| `INotificationService`| Шлёт email через SMTP                  | Нельзя слать письма в тестах         |
| `IIdempotencyStore`   | Кэширует результат (Redis / БД / etc.) | Внешнее хранилище, недетерминировано |

Moq позволяет заменить все пять систем на контролируемые заглушки,  
благодаря чему тест проверяет **только бизнес-логику**, а не инфраструктуру.

---

## Правила написания тестов

Все тесты в проекте следуют пяти явным правилам:

### 1. Изоляция через поля класса

Моки объявлены как поля и инициализируются в конструкторе.  
xUnit создаёт **новый экземпляр класса** для каждого `[Fact]`,  
поэтому никакого shared state между тестами нет.

```csharp
public class OrderServiceTests
{
    private readonly Mock<IPaymentGateway> _paymentMock;

    public OrderServiceTests()
    {
        _paymentMock = new Mock<IPaymentGateway>(); // свежий мок на каждый тест
        // ...
    }
}
```

### 2. Формат имён `MethodUnderTest_Scenario_ExpectedResult`

```text
CreateOrderAsync_ValidDtoWithTwoProducts_ReturnsSuccessResult
CreateOrderAsync_PaymentDeclined_NeverSavesOrder
CreateOrderAsync_SameIdempotencyKeyCalledTwice_ChargesPaymentOnlyOnce
```

### 3. AAA-паттерн с явными комментариями

```csharp
[Fact]
public async Task CreateOrderAsync_ProductNotFound_NeverChargesPayment()
{
    // Arrange
    SetupProductNotFound(productId: 1);
    var dto = new CreateOrderDto { ... };

    // Act
    await _sut.CreateOrderAsync(dto, CancellationToken.None);

    // Assert
    _paymentMock.Verify(
        p => p.ChargeAsync(...),
        Times.Never);
}
```

### 4. `Verify` с `Times.Once` / `Times.Never` / `Times.Exactly`

Все проверки вызовов делаются через `Verify`, а не только через возвращаемые значения.

### 5. `It.Is<T>(predicate)` для значимых аргументов

Там, где конкретное значение аргумента важно для теста, используется предикат,  
а не `It.IsAny` - это делает намерение теста явным.

```csharp
_paymentMock.Verify(
    p => p.ChargeAsync(
        It.Is<decimal>(amount => amount == 45m),  // проверяем именно сумму
        It.Is<string>(token   => token == CardToken),
        It.IsAny<CancellationToken>()),
    Times.Once);
```

---

## Как Moq используется здесь

### Создание мока и Setup

```csharp
var productRepo = new Mock<IProductRepository>();

productRepo
    .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Product { Id = 1, Name = "Widget", Price = 10m, StockQuantity = 10 });
```

В проекте для каждой зависимости есть отдельный Setup-хелпер:

```csharp
private void SetupProduct(Product product) =>
    _productRepoMock
        .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(product);

private void SetupPaymentSuccess(string txId = "tx_001") =>
    _paymentMock
        .Setup(p => p.ChargeAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(PaymentResult.Succeeded(txId));
```

---

### It.Is\<T\> - точный матчинг аргументов

`It.IsAny<T>()` принимает любое значение - удобно в `Setup`.  
`It.Is<T>(predicate)` принимает только значения, удовлетворяющие предикату - используется в `Verify`, чтобы утверждение было точным:

```csharp
// Убеждаемся, что списана именно правильная сумма:
_paymentMock.Verify(
    p => p.ChargeAsync(
        It.Is<decimal>(amount => amount == 45m),
        It.Is<string>(token   => token == CardToken),
        It.IsAny<CancellationToken>()),
    Times.Once);
```

---

### Callback - захват аргументов и имитация состояния

`Callback` срабатывает в момент вызова мока. Используется для «подглядывания»  
за аргументами, которые сервис передаёт зависимости:

```csharp
Order? capturedOrder = null;

_orderRepoMock
    .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .Callback<Order, CancellationToken>((order, _) => capturedOrder = order)
    .ReturnsAsync(SavedOrderId);

// После вызова сервиса - проверяем сохранённый объект:
capturedOrder!.Status.Should().Be(OrderStatus.Paid);
capturedOrder.TotalAmount.Should().Be(45m);
```

---

### Verify - проверка количества вызовов

`Times.Never` / `Times.Once` / `Times.Exactly(n)` - контроль **количества вызовов**,  
а не только возвращаемого значения:

```csharp
// Платёж НЕ должен вызываться, если товар не найден:
_paymentMock.Verify(
    p => p.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
    Times.Never);

// DecreaseStock должен вызваться ровно для каждой позиции:
_productRepoMock.Verify(
    r => r.DecreaseStockAsync(It.Is<int>(id => id == 1), It.Is<int>(qty => qty == 2),
    It.IsAny<CancellationToken>()),
    Times.Once);
```

---

### Замыкание для имитации кэша (идемпотентность)

Самый интересный паттерн в проекте. Задача: вызвать сервис дважды в одном тесте  
и проверить, что второй вызов возвращает результат из «кэша» без повторных побочных эффектов.

Проблема: `GetAsync` должен вернуть `null` при первом вызове и закешированный результат - при втором.  
Решение: переменная `stored` захватывается в замыкание, разделённое между двумя лямбдами.

```csharp
private void SetupIdempotencyStore(string key)
{
    CreateOrderResult? stored = null;           // переменная в замыкании

    _idempotencyStoreMock
        .Setup(s => s.GetAsync(
            It.Is<string>(k => k == key),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(() => stored);            // ленивое чтение: при первом вызове → null,
                                                // после StoreAsync → объект

    _idempotencyStoreMock
        .Setup(s => s.StoreAsync(
            It.Is<string>(k => k == key),
            It.IsAny<CreateOrderResult>(),
            It.IsAny<CancellationToken>()))
        .Callback<string, CreateOrderResult, CancellationToken>(
            (_, result, _) => stored = result) // запись в замыкание имитирует кэш
        .Returns(Task.CompletedTask);
}
```

Ключевой момент: `ReturnsAsync(() => stored)` - это **фабричная лямбда**, а не `ReturnsAsync(stored)`.  
Без `() =>` Moq захватил бы значение `null` в момент Setup и вернул бы его всегда.  
С `() =>` возвращаемое значение вычисляется при каждом вызове - поэтому второй `GetAsync`  
видит уже записанный `Callback`'ом результат.

```csharp
[Fact]
public async Task CreateOrderAsync_SameIdempotencyKeyCalledTwice_ChargesPaymentOnlyOnce()
{
    // Arrange
    const string key = "idem-key-001";
    SetupProduct(MakeProductA());
    SetupProduct(MakeProductB());
    SetupPaymentSuccess();
    SetupSaveOrder();
    SetupDecreaseStock();
    SetupSendNotification();
    SetupIdempotencyStore(key);                         // настраиваем кэш-замыкание
    var dto = IdempotentTwoProductDto(key);

    // Act
    await _sut.CreateOrderAsync(dto, CancellationToken.None); // полный флоу
    await _sut.CreateOrderAsync(dto, CancellationToken.None); // возврат из кэша

    // Assert
    _paymentMock.Verify(
        p => p.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
        Times.Once);                                    // списание произошло ровно один раз
}
```

---

## Покрытые сценарии

### Группа 1 - Happy path (успешный заказ)

| # | Название теста | Что проверяем |
| - | -------------- | ------------- |
| 1 | `ValidDtoWithTwoProducts_ReturnsSuccessResult` | `Success = true`, верный `OrderId`, `Error = null` |
| 2 | `ValidDtoWithTwoProducts_ChargesCorrectTotalAmount` | Списана правильная сумма (2×10 + 1×25 = 45) |
| 3 | `ValidDtoWithTwoProducts_SavesOrderWithPaidStatus` | Заказ сохранён со статусом `Paid`, `TotalAmount = 45` |
| 4 | `ValidDtoWithTwoProducts_SavesOrderWithCorrectLineItems` | Позиции заказа с верными `ProductId`, `Quantity`, `UnitPrice` |
| 5 | `ValidDtoWithTwoProducts_DecreasesStockForEachProduct` | `DecreaseStockAsync` вызван по разу для каждой позиции |
| 6 | `ValidDtoWithTwoProducts_SendsConfirmationToCustomerEmail` | Email отправлен с правильным адресом и `OrderId` |

### Группа 2 - Товар не найден

| # | Название теста | Что проверяем |
| - | -------------- | ------------- |
| 7 | `ProductNotFound_ReturnsFailResult` | `Success = false`, сообщение об ошибке, `OrderId = null` |
| 8 | `ProductNotFound_NeverChargesPayment` | `ChargeAsync` - `Times.Never` |
| 9 | `SecondProductNotFound_NeverChargesPayment` | Та же гарантия при нескольких позициях |

### Группа 3 - Нехватка остатка

| # | Название теста | Что проверяем |
| - | -------------- | ------------- |
| 10 | `InsufficientStock_ReturnsFailResult` | `Success = false`, сообщение об ошибке |
| 11 | `InsufficientStock_NeverChargesPayment` | `ChargeAsync` - `Times.Never` |
| 12 | `StockEqualsRequestedQuantity_ReturnsSuccessResult` | Граница «ровно хватает» → `Success = true` |
| 13 | `StockLessThanRequestedByOne_ReturnsFailResult` | Граница «не хватает на 1» → `Success = false` |

### Группа 4 - Платёж отклонён

| # | Название теста | Что проверяем |
| - | -------------- | ------------- |
| 14 | `PaymentDeclined_ReturnsFailResultWithGatewayError` | `Success = false`, сообщение содержит текст от шлюза |
| 15 | `PaymentDeclined_NeverSavesOrder` | `SaveAsync` - `Times.Never` |
| 16 | `PaymentDeclined_NeverDecreasesStock` | `DecreaseStockAsync` - `Times.Never` |
| 17 | `PaymentDeclined_NeverSendsNotification` | `SendOrderConfirmationAsync` - `Times.Never` |

### Группа 5 - Корректность расчёта суммы

| # | Название теста | Что проверяем |
| - | -------------- | ------------- |
| 18 | `SingleProductThreeUnits_ChargesCorrectTotal` | 7.50 × 3 = 22.50, токен карты совпадает |

### Группа 6 - Идемпотентность (`IdempotencyKey`)

| # | Название теста | Что проверяем |
| - | -------------- | ------------- |
| 19 | `SameIdempotencyKeyCalledTwice_ReturnsOriginalOrderIdOnSecondCall` | Второй вызов возвращает `OrderId` первого |
| 20 | `SameIdempotencyKeyCalledTwice_ChargesPaymentOnlyOnce` | `ChargeAsync` - `Times.Once` при двух вызовах |
| 21 | `SameIdempotencyKeyCalledTwice_SavesOrderOnlyOnce` | `SaveAsync` - `Times.Once` при двух вызовах |
| 22 | `SameIdempotencyKeyCalledTwice_SendsNotificationOnlyOnce` | `SendOrderConfirmationAsync` - `Times.Once` при двух вызовах |
| 23 | `WithoutIdempotencyKey_NeverQueriesIdempotencyStore` | `GetAsync` и `StoreAsync` - оба `Times.Never`, если ключ не передан |
| 24 | `DifferentIdempotencyKeys_ProcessesEachKeyIndependently` | Два разных ключа → два независимых полных флоу (`Times.Exactly(2)`) |

---

## Минусы Moq

### 1. Тесты проверяют реализацию, а не поведение

Когда мы пишем `payment.Verify(..., Times.Once)`, тест привязывается  
к тому, **как** написан код, а не к тому, **что** он делает.  
Стоит переименовать метод или изменить сигнатуру - тест сломается,  
хотя бизнес-логика осталась корректной.

---

### 2. Моки не воспроизводят реальное поведение зависимости

Мок `IOrderRepository` вернёт `id = 99` всегда.  
Настоящая БД вернёт ошибку при дублирующем ключе, при дедлоке,  
при переполнении поля - обо всём этом моки молчат.  
Такие сценарии нужно покрывать интеграционными тестами.

---

### 3. Взрывной рост бойлерплейта

Чем больше у сервиса зависимостей, тем длиннее блок `Setup`.  
В этом проекте уже **5 зависимостей** и столько же Setup-хелперов.  
При 6–8 зависимостях тест-файл становится трудночитаемым - это  
сигнал пересмотреть дизайн самого сервиса.

---

### 4. `MockBehavior.Strict` легко даёт ложные провалы

Если поведение сервиса изменилось (например, добавился новый вызов  
метода интерфейса), строгий мок падает с `MockException: Invocation ... was unexpected`.  
Это иногда запутывает: падение выглядит как ошибка теста, хотя логика верна.  
По умолчанию `MockBehavior.Loose` - незаданные методы возвращают `default`.

---

### 5. Нельзя мокать статические методы и `sealed`-классы

Moq генерирует прокси через наследование / реализацию интерфейса.  
Статические вызовы (`DateTime.UtcNow`, `File.ReadAllText`) и  
запечатанные классы без интерфейса требуют либо обёртки-фасада,  
либо других инструментов (**Microsoft Fakes** для `sealed`).

---

### 6. Порядок вызовов не проверяется «из коробки»

По условию задачи: сначала платёж, потом сохранение, потом уменьшение остатков.  
Moq не проверяет порядок по умолчанию.  
Для этого нужен `MockSequence` - громоздкий API, который большинство команд избегает.

```csharp
// Moq MockSequence - нечитаемо при большом числе шагов:
var seq = new MockSequence();
payment.InSequence(seq).Setup(...);
orderRepo.InSequence(seq).Setup(...);
```

Альтернатива - **вручную собирать лог вызовов через Callback**  
и потом утверждать порядок через `FluentAssertions`.

---

### Итог: когда Moq оправдан, а когда нет

| Ситуация | Рекомендация |
| -------- | ----------- |
| Сервис с 2–5 интерфейсными зависимостями | ✅ Moq - отличный выбор |
| Нужно проверить, что метод **не** вызвался | ✅ `Times.Never` - удобно |
| Нужно проверить идемпотентность с состоянием | ✅ Замыкание + `ReturnsAsync(() => val)` |
| Зависимость - конкретный `sealed`-класс | ⚠️ Нужна обёртка или другой инструмент |
| Нужно проверить строгий порядок шагов | ⚠️ Сложно, рассмотрите ручной лог |
| Сложная логика самой зависимости важна | ❌ Используйте интеграционные тесты |
| 8+ зависимостей в одном сервисе | ❌ Это сигнал к рефакторингу сервиса |

---

## Запуск тестов

```bash
# Сборка
dotnet build

# Все тесты
dotnet test

# С подробным выводом
dotnet test -v normal

# Только одна группа (по части имени)
dotnet test --filter "Idempotency"

# Только один конкретный тест
dotnet test --filter "CreateOrderAsync_PaymentDeclined_NeverSavesOrder"
```

Ожидаемый результат:

```text
Total tests: 24
     Passed: 24
```
