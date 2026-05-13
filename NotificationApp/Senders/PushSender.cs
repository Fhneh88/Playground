using Notifications.Interfaces;

namespace Notifications.Senders;

public class PushSender : INotificationSender
{
    public Task SendAsync(string recipient, string message)
    {
        Console.WriteLine($"[Push] Отправлено на {recipient}: {message}");
        return Task.CompletedTask;
    }
}
