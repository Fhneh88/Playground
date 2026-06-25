using Microsoft.Extensions.Logging;
using OrderService.Core.Interfaces;
using OrderService.Core.Models;

namespace OrderService.Core.Services;

public class OrderService
{
    private readonly IOrderRepository      _orderRepo;
    private readonly IProductRepository    _productRepo;
    private readonly IPaymentGateway       _payment;
    private readonly INotificationService  _notifications;
    private readonly IIdempotencyStore     _idempotencyStore;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository      orderRepo,
        IProductRepository    productRepo,
        IPaymentGateway       payment,
        INotificationService  notifications,
        ILogger<OrderService> logger,
        IIdempotencyStore     idempotencyStore)
    {
        _orderRepo        = orderRepo;
        _productRepo      = productRepo;
        _payment          = payment;
        _notifications    = notifications;
        _logger           = logger;
        _idempotencyStore = idempotencyStore;
    }

    /// <summary>
    /// Создаёт заказ:
    ///   0. Если передан IdempotencyKey - проверяет кэш; при совпадении возвращает
    ///      сохранённый результат без повторной обработки.
    ///   1. Проверяет наличие всех товаров и достаточность остатков.
    ///   2. Считает итоговую сумму.
    ///   3. Списывает деньги через платёжный шлюз.
    ///   4. Сохраняет заказ со статусом Paid.
    ///   5. Уменьшает остатки товаров.
    ///   6. Отправляет подтверждение на e-mail.
    ///   7. Сохраняет успешный результат в хранилище идемпотентности.
    ///
    /// Если товар недоступен - возвращает ошибку, не трогая платёж.
    /// Если платёж не прошёл - заказ не создаётся.
    /// Ошибочные результаты НЕ кэшируются.
    /// </summary>
    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderDto dto,
        CancellationToken ct = default)
    {
        // ── 0. Проверка идемпотентности ──────────────────────────────────
        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            var cached = await _idempotencyStore.GetAsync(dto.IdempotencyKey, ct);
            if (cached is not null)
            {
                _logger.LogInformation(
                    "Idempotent replay for key {Key}: returning cached OrderId {OrderId}",
                    dto.IdempotencyKey, cached.OrderId);
                return cached;
            }
        }

        // ── 1. Проверка наличия товаров ──────────────────────────────────
        var productCache = new Dictionary<int, Product>(dto.Items.Count);

        foreach (var line in dto.Items)
        {
            var product = await _productRepo.GetByIdAsync(line.ProductId, ct);

            if (product is null)
            {
                _logger.LogWarning("Product {ProductId} not found", line.ProductId);
                return CreateOrderResult.Fail($"Товар {line.ProductId} не найден.");
            }

            if (product.StockQuantity < line.Quantity)
            {
                _logger.LogWarning(
                    "Product {ProductId} has insufficient stock: required {Required}, available {Available}",
                    line.ProductId, line.Quantity, product.StockQuantity);

                return CreateOrderResult.Fail(
                    $"Недостаточно товара «{product.Name}»: " +
                    $"запрошено {line.Quantity}, доступно {product.StockQuantity}.");
            }

            productCache[product.Id] = product;
        }

        // ── 2. Подсчёт суммы ─────────────────────────────────────────────
        var total = dto.Items.Sum(line => productCache[line.ProductId].Price * line.Quantity);

        _logger.LogInformation("Charging {Total} for order by {Email}", total, dto.CustomerEmail);

        // ── 3. Платёж ─────────────────────────────────────────────────────
        var paymentResult = await _payment.ChargeAsync(total, dto.CardToken, ct);

        if (!paymentResult.Success)
        {
            _logger.LogWarning("Payment failed: {Error}", paymentResult.Error);
            return CreateOrderResult.Fail($"Платёж отклонён: {paymentResult.Error}");
        }

        // ── 4. Сохранение заказа ──────────────────────────────────────────
        var order = new Order
        {
            CustomerEmail = dto.CustomerEmail,
            TotalAmount   = total,
            Status        = OrderStatus.Paid,
            CreatedAt     = DateTime.UtcNow,
            Items = dto.Items
                .Select(line => new OrderItem
                {
                    ProductId = line.ProductId,
                    Quantity  = line.Quantity,
                    UnitPrice = productCache[line.ProductId].Price
                })
                .ToList()
        };

        var orderId = await _orderRepo.SaveAsync(order, ct);
        _logger.LogInformation("Order {OrderId} saved with status Paid", orderId);

        // ── 5. Уменьшение остатков ────────────────────────────────────────
        foreach (var line in dto.Items)
            await _productRepo.DecreaseStockAsync(line.ProductId, line.Quantity, ct);

        // ── 6. Уведомление ────────────────────────────────────────────────
        await _notifications.SendOrderConfirmationAsync(dto.CustomerEmail, orderId, ct);

        // ── 7. Кэширование успешного результата ───────────────────────────
        var result = CreateOrderResult.Ok(orderId);

        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
            await _idempotencyStore.StoreAsync(dto.IdempotencyKey, result, ct);

        return result;
    }
}
