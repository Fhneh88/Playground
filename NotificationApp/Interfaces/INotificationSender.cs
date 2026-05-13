namespace Notifications.Interfaces;

public interface INotificationSender
{
    Task SendAsync(string recipient, string message);
}
