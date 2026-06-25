using OrderService.Core.Models;

namespace OrderService.Core.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken ct);
    Task           DecreaseStockAsync(int productId, int quantity, CancellationToken ct);
}
