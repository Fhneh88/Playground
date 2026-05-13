using Notifications.Interfaces;

namespace Notifications.Services;

public class NotificationService
{
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly INotificationFilter _filter;

    public NotificationService(
        IEnumerable<INotificationSender> senders,
        INotificationFilter filter)
    {
        _senders = senders;
        _filter = filter;
    }

    public async Task NotifyAllAsync(string recipient, string message)
    {
        if (!_filter.ShouldSend(recipient, message))
        {
            Console.WriteLine($"[Filter] Сообщение заблокировано для {recipient}: \"{message}\"");
            return;
        }

        foreach (var sender in _senders)
        {
            await sender.SendAsync(recipient, message);
        }
    }
}
