namespace OrderService.Core.Interfaces;

public interface INotificationService
{
    Task SendOrderConfirmationAsync(string email, int orderId, CancellationToken ct);
}
