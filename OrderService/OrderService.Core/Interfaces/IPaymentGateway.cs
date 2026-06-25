using OrderService.Core.Models;

namespace OrderService.Core.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(decimal amount, string cardToken, CancellationToken ct);
}
