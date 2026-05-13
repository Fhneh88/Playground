using Notifications.Interfaces;

namespace Notifications.Filters;

// Registered as Singleton because:
// - the banned-words list is static, shared state that never changes at runtime
// - creating a new instance per request would waste memory for no gain
// - the class is stateless beyond the read-only set, so it is inherently thread-safe
public class ProfanityFilter : INotificationFilter
{
    private readonly HashSet<string> _bannedWords =
        ["spam", "scam", "запрещено", "prohibited"];

    public bool ShouldSend(string recipient, string message)
    {
        return !_bannedWords.Any(word =>
            message.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
