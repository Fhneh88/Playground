using Notifications.Interfaces;

namespace Notifications.Senders;

public class SmsSender : INotificationSender
{
    public Task SendAsync(string recipient, string message)
    {
        Console.WriteLine($"[SMS] Отправлено на {recipient}: {message}");
        return Task.CompletedTask;
    }
}
