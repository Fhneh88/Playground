using OrderService.Core.Models;

namespace OrderService.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken ct);
    Task<int>    SaveAsync(Order order, CancellationToken ct);
    Task         UpdateStatusAsync(int id, OrderStatus status, CancellationToken ct);
}
