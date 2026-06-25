using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Core.Interfaces;
using OrderService.Core.Models;

namespace OrderService.Tests;

/// <summary>
/// Юнит-тесты OrderService.CreateOrderAsync.
///
/// Правила:
///   • Моки - поля класса; xUnit создаёт новый экземпляр на каждый тест → shared state исключён.
///   • Имена:  MethodUnderTest_Scenario_ExpectedResult.
///   • Структура каждого теста: // Arrange / // Act / // Assert.
///   • Проверка вызовов: Verify + Times.Once / Times.Never.
///   • Матчинг аргументов: It.Is&lt;T&gt;(predicate) там, где значение имеет смысл.
/// </summary>
public class OrderServiceTests
{
    // ── Константы ────────────────────────────────────────────────────────
    private const string CustomerEmail = "buyer@example.com";
    private const string CardToken     = "tok_visa_4242";
    private const int    SavedOrderId  = 99;

    // ── Моки-поля: xUnit создаёт новый экземпляр класса на каждый тест,
    //    поэтому каждый тест получает собственные, независимые экземпляры. ──
    private readonly Mock<IOrderRepository>    _orderRepoMock;
    private readonly Mock<IProductRepository>  _productRepoMock;
    private readonly Mock<IPaymentGateway>     _paymentMock;
    private readonly Mock<INotificationService> _notificationsMock;
    private readonly Mock<IIdempotencyStore>   _idempotencyStoreMock;
    private readonly Core.Services.OrderService _sut;

    public OrderServiceTests()
    {
        _orderRepoMock        = new Mock<IOrderRepository>();
        _productRepoMock      = new Mock<IProductRepository>();
        _paymentMock          = new Mock<IPaymentGateway>();
        _notificationsMock    = new Mock<INotificationService>();
        _idempotencyStoreMock = new Mock<IIdempotencyStore>();

        _sut = new Core.Services.OrderService(
            _orderRepoMock.Object,
            _productRepoMock.Object,
            _paymentMock.Object,
            _notificationsMock.Object,
            NullLogger<Core.Services.OrderService>.Instance,
            _idempotencyStoreMock.Object);
    }

    // ── Фабрики тестовых данных ───────────────────────────────────────────
    private static Product MakeProductA() =>
        new() { Id = 1, Name = "Widget", Price = 10m, StockQuantity = 10 };

    private static Product MakeProductB() =>
        new() { Id = 2, Name = "Gadget", Price = 25m, StockQuantity = 5 };

    /// <summary>DTO: 2×Widget(10) + 1×Gadget(25) = 45 итого.</summary>
    private static CreateOrderDto TwoProductDto() => new()
    {
        CustomerEmail = CustomerEmail,
        CardToken     = CardToken,
        Items =
        [
            new OrderLineDto { ProductId = 1, Quantity = 2 },
            new OrderLineDto { ProductId = 2, Quantity = 1 }
        ]
    };

    // ── Setup-хелперы (возвращают this для fluent-стиля) ─────────────────
    private void SetupProduct(Product product) =>
        _productRepoMock
            .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

    private void SetupProductNotFound(int productId) =>
        _productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

    private void SetupDecreaseStock() =>
        _productRepoMock
            .Setup(r => r.DecreaseStockAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupPaymentSuccess(string txId = "tx_001") =>
        _paymentMock
            .Setup(p => p.ChargeAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Succeeded(txId));

    private void SetupPaymentFailure(string error = "Declined") =>
        _paymentMock
            .Setup(p => p.ChargeAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Failed(error));

    private void SetupSaveOrder(int orderId = SavedOrderId) =>
        _orderRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderId);

    private void SetupSendNotification() =>
        _notificationsMock
            .Setup(n => n.SendOrderConfirmationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    /// <summary>
    /// Настраивает хранилище идемпотентности как «in-memory кэш» через замыкание.
    ///
    /// Ключевой приём - переменная <c>stored</c> общая для обоих лямбд:
    ///   • <c>GetAsync</c> → <c>() =&gt; stored</c>: ленивое чтение -
    ///     при первом вызове возвращает <c>null</c>, после <c>StoreAsync</c> - объект.
    ///   • <c>StoreAsync</c> → Callback записывает результат в <c>stored</c>,
    ///     имитируя реальную запись без какого-либо внешнего состояния.
    ///
    /// Это позволяет вызвать <c>_sut.CreateOrderAsync</c> дважды внутри
    /// одного теста и наблюдать поведение второго (идемпотентного) вызова.
    /// </summary>
    private void SetupIdempotencyStore(string key)
    {
        CreateOrderResult? stored = null;

        _idempotencyStoreMock
            .Setup(s => s.GetAsync(
                It.Is<string>(k => k == key),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);                               // ленивое чтение замыкания

        _idempotencyStoreMock
            .Setup(s => s.StoreAsync(
                It.Is<string>(k => k == key),
                It.IsAny<CreateOrderResult>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, CreateOrderResult, CancellationToken>(
                (_, result, _) => stored = result)                    // запись в замыкание
            .Returns(Task.CompletedTask);
    }

    /// <summary>DTO с IdempotencyKey: те же два товара, что и TwoProductDto.</summary>
    private static CreateOrderDto IdempotentTwoProductDto(string key) => new()
    {
        CustomerEmail  = CustomerEmail,
        CardToken      = CardToken,
        IdempotencyKey = key,
        Items =
        [
            new OrderLineDto { ProductId = 1, Quantity = 2 },
            new OrderLineDto { ProductId = 2, Quantity = 1 }
        ]
    };

    // ════════════════════════════════════════════════════════════════════
    // 1. Happy path
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrderAsync_ValidDtoWithTwoProducts_ReturnsSuccessResult()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();

        // Act
        var result = await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderId.Should().Be(SavedOrderId);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderAsync_ValidDtoWithTwoProducts_ChargesCorrectTotalAmount()
    {
        // Arrange - 2×10 + 1×25 = 45
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupDecreaseStock();
        SetupSaveOrder();
        SetupSendNotification();
        SetupPaymentSuccess();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.Is<decimal>(amount => amount == 45m),
                It.Is<string>(token  => token  == CardToken),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidDtoWithTwoProducts_SavesOrderWithPaidStatus()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupDecreaseStock();
        SetupSendNotification();

        Order? capturedOrder = null;
        _orderRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => capturedOrder = order)
            .ReturnsAsync(SavedOrderId);

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(
            r => r.SaveAsync(
                It.Is<Order>(o =>
                    o.Status        == OrderStatus.Paid &&
                    o.CustomerEmail == CustomerEmail    &&
                    o.TotalAmount   == 45m),
                It.IsAny<CancellationToken>()),
            Times.Once);

        capturedOrder!.Status.Should().Be(OrderStatus.Paid);
        capturedOrder.TotalAmount.Should().Be(45m);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidDtoWithTwoProducts_SavesOrderWithCorrectLineItems()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupDecreaseStock();
        SetupSendNotification();

        Order? capturedOrder = null;
        _orderRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => capturedOrder = order)
            .ReturnsAsync(SavedOrderId);

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        capturedOrder!.Items.Should().HaveCount(2);
        capturedOrder.Items.Should().ContainSingle(i =>
            i.ProductId == 1 && i.Quantity == 2 && i.UnitPrice == 10m);
        capturedOrder.Items.Should().ContainSingle(i =>
            i.ProductId == 2 && i.Quantity == 1 && i.UnitPrice == 25m);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidDtoWithTwoProducts_DecreasesStockForEachProduct()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _productRepoMock.Verify(
            r => r.DecreaseStockAsync(
                It.Is<int>(id  => id  == 1),
                It.Is<int>(qty => qty == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _productRepoMock.Verify(
            r => r.DecreaseStockAsync(
                It.Is<int>(id  => id  == 2),
                It.Is<int>(qty => qty == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidDtoWithTwoProducts_SendsConfirmationToCustomerEmail()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _notificationsMock.Verify(
            n => n.SendOrderConfirmationAsync(
                It.Is<string>(email => email == CustomerEmail),
                It.Is<int>(id       => id    == SavedOrderId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ════════════════════════════════════════════════════════════════════
    // 2. Товар не найден - ошибка возвращается до вызова платежа
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrderAsync_ProductNotFound_ReturnsFailResult()
    {
        // Arrange
        SetupProductNotFound(productId: 1);

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 1, Quantity = 1 }]
        };

        // Act
        var result = await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.OrderId.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderAsync_ProductNotFound_NeverChargesPayment()
    {
        // Arrange
        SetupProductNotFound(productId: 1);

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 1, Quantity = 1 }]
        };

        // Act
        await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_SecondProductNotFound_NeverChargesPayment()
    {
        // Arrange - первый товар в наличии, второй отсутствует
        SetupProduct(MakeProductA());
        SetupProductNotFound(productId: 2);

        // Act
        var result = await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();

        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ════════════════════════════════════════════════════════════════════
    // 3. Нехватка остатка - ошибка возвращается до вызова платежа
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrderAsync_InsufficientStock_ReturnsFailResult()
    {
        // Arrange - stock = 1, requested = 5
        var product = new Product { Id = 7, Name = "Rare", Price = 100m, StockQuantity = 1 };
        SetupProduct(product);

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 7, Quantity = 5 }]
        };

        // Act
        var result = await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateOrderAsync_InsufficientStock_NeverChargesPayment()
    {
        // Arrange
        var product = new Product { Id = 7, Name = "Rare", Price = 100m, StockQuantity = 1 };
        SetupProduct(product);

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 7, Quantity = 999 }]
        };

        // Act
        await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_StockEqualsRequestedQuantity_ReturnsSuccessResult()
    {
        // Arrange - граница: stock == qty → должно пройти
        var product = new Product { Id = 5, Name = "Edge", Price = 1m, StockQuantity = 3 };
        SetupProduct(product);
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 5, Quantity = 3 }]
        };

        // Act
        var result = await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateOrderAsync_StockLessThanRequestedByOne_ReturnsFailResult()
    {
        // Arrange - граница: stock = qty − 1 → должно упасть
        var product = new Product { Id = 5, Name = "Edge", Price = 1m, StockQuantity = 3 };
        SetupProduct(product);

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 5, Quantity = 4 }]
        };

        // Act
        var result = await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════
    // 4. Платёж отклонён - заказ не создаётся, побочных эффектов нет
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrderAsync_PaymentDeclined_ReturnsFailResultWithGatewayError()
    {
        // Arrange
        const string gatewayError = "Insufficient funds";
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentFailure(gatewayError);

        // Act
        var result = await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain(gatewayError);
        result.OrderId.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderAsync_PaymentDeclined_NeverSavesOrder()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentFailure();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(
            r => r.SaveAsync(
                It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_PaymentDeclined_NeverDecreasesStock()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentFailure();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _productRepoMock.Verify(
            r => r.DecreaseStockAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_PaymentDeclined_NeverSendsNotification()
    {
        // Arrange
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentFailure();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _notificationsMock.Verify(
            n => n.SendOrderConfirmationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ════════════════════════════════════════════════════════════════════
    // 5. Корректность расчёта суммы
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrderAsync_SingleProductThreeUnits_ChargesCorrectTotal()
    {
        // Arrange - price=7.50, qty=3 → total=22.50
        var product = new Product { Id = 3, Name = "Bolt", Price = 7.50m, StockQuantity = 100 };
        SetupProduct(product);
        SetupDecreaseStock();
        SetupSaveOrder(orderId: 1);
        SetupSendNotification();

        _paymentMock
            .Setup(p => p.ChargeAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Succeeded("tx_002"));

        var dto = new CreateOrderDto
        {
            CustomerEmail = CustomerEmail,
            CardToken     = CardToken,
            Items         = [new OrderLineDto { ProductId = 3, Quantity = 3 }]
        };

        // Act
        await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.Is<decimal>(amount => amount == 22.50m),
                It.Is<string>(token   => token   == CardToken),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ════════════════════════════════════════════════════════════════════
    // 6. Идемпотентность (IdempotencyKey)
    //
    // Принцип проверки: один и тот же _sut вызывается дважды подряд с одним ключом.
    // SetupIdempotencyStore имитирует кэш через замыкание - первый вызов
    // проходит полный флоу, второй получает результат из «кэша».
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrderAsync_SameIdempotencyKeyCalledTwice_ReturnsOriginalOrderIdOnSecondCall()
    {
        // Arrange
        const string key = "idem-key-001";
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();
        SetupIdempotencyStore(key);
        var dto = IdempotentTwoProductDto(key);

        // Act
        var firstResult  = await _sut.CreateOrderAsync(dto, CancellationToken.None);
        var secondResult = await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        secondResult.Success.Should().BeTrue();
        secondResult.OrderId.Should().Be(firstResult.OrderId);
    }

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
        SetupIdempotencyStore(key);
        var dto = IdempotentTwoProductDto(key);

        // Act
        await _sut.CreateOrderAsync(dto, CancellationToken.None);
        await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_SameIdempotencyKeyCalledTwice_SavesOrderOnlyOnce()
    {
        // Arrange
        const string key = "idem-key-001";
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();
        SetupIdempotencyStore(key);
        var dto = IdempotentTwoProductDto(key);

        // Act
        await _sut.CreateOrderAsync(dto, CancellationToken.None);
        await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        _orderRepoMock.Verify(
            r => r.SaveAsync(
                It.IsAny<Order>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_SameIdempotencyKeyCalledTwice_SendsNotificationOnlyOnce()
    {
        // Arrange
        const string key = "idem-key-001";
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();
        SetupIdempotencyStore(key);
        var dto = IdempotentTwoProductDto(key);

        // Act
        await _sut.CreateOrderAsync(dto, CancellationToken.None);
        await _sut.CreateOrderAsync(dto, CancellationToken.None);

        // Assert
        _notificationsMock.Verify(
            n => n.SendOrderConfirmationAsync(
                It.Is<string>(email => email == CustomerEmail),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithoutIdempotencyKey_NeverQueriesIdempotencyStore()
    {
        // Arrange - TwoProductDto() не содержит IdempotencyKey (null)
        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();

        // Act
        await _sut.CreateOrderAsync(TwoProductDto(), CancellationToken.None);

        // Assert
        _idempotencyStoreMock.Verify(
            s => s.GetAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _idempotencyStoreMock.Verify(
            s => s.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<CreateOrderResult>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_DifferentIdempotencyKeys_ProcessesEachKeyIndependently()
    {
        // Arrange - два разных ключа: оба должны пройти полный флоу
        const string keyA = "idem-key-A";
        const string keyB = "idem-key-B";

        SetupProduct(MakeProductA());
        SetupProduct(MakeProductB());
        SetupPaymentSuccess();
        SetupSaveOrder();
        SetupDecreaseStock();
        SetupSendNotification();
        SetupIdempotencyStore(keyA);
        SetupIdempotencyStore(keyB);

        // Act
        await _sut.CreateOrderAsync(IdempotentTwoProductDto(keyA), CancellationToken.None);
        await _sut.CreateOrderAsync(IdempotentTwoProductDto(keyB), CancellationToken.None);

        // Assert - каждый ключ обработан независимо
        _paymentMock.Verify(
            p => p.ChargeAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _orderRepoMock.Verify(
            r => r.SaveAsync(
                It.IsAny<Order>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
