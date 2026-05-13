using Notifications.Interfaces;

namespace Notifications.Senders;

public class EmailSender : INotificationSender
{
    public Task SendAsync(string recipient, string message)
    {
        Console.WriteLine($"[Email] Отправлено на {recipient}: {message}");
        return Task.CompletedTask;
    }
}
