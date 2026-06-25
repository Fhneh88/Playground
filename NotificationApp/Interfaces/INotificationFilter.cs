namespace Notifications.Interfaces;

public interface INotificationFilter
{
    bool ShouldSend(string recipient, string message);
}
