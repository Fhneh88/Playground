using OrderService.Core.Models;

namespace OrderService.Core.Interfaces;

/// <summary>
/// Хранилище результатов для идемпотентных вызовов.
/// Реализация может быть in-memory, Redis, БД - сервис не знает деталей.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Возвращает ранее сохранённый результат по ключу,
    /// или <c>null</c>, если ключ не найден (первый вызов).
    /// </summary>
    Task<CreateOrderResult?> GetAsync(string key, CancellationToken ct);

    /// <summary>Сохраняет успешный результат под ключом.</summary>
    Task StoreAsync(string key, CreateOrderResult result, CancellationToken ct);
}
