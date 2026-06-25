namespace OrderService.Core.Models;

/// <summary>Запрос на создание заказа.</summary>
public class CreateOrderDto
{
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>Токен карты, передаётся в платёжный шлюз.</summary>
    public string CardToken { get; set; } = string.Empty;

    public List<OrderLineDto> Items { get; set; } = new();

    /// <summary>
    /// Опциональный ключ идемпотентности (UUID, сгенерированный клиентом).
    /// Если задан, повторный вызов с тем же ключом вернёт результат первого вызова
    /// без повторного списания и создания заказа.
    /// </summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Одна позиция в заказе: товар + количество.</summary>
public class OrderLineDto
{
    public int ProductId { get; set; }
    public int Quantity  { get; set; }
}
