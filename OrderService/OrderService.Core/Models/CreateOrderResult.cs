namespace OrderService.Core.Models;

/// <summary>Результат создания заказа (паттерн Result).</summary>
public sealed class CreateOrderResult
{
    public bool    Success { get; private set; }
    public int?    OrderId { get; private set; }
    public string? Error   { get; private set; }

    private CreateOrderResult() { }

    public static CreateOrderResult Ok(int orderId) =>
        new() { Success = true, OrderId = orderId };

    public static CreateOrderResult Fail(string error) =>
        new() { Success = false, Error = error };
}
