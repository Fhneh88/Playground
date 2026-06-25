namespace Consumer;

public class ProcessedMessages
{
    private readonly HashSet<Guid> _seen = [];

    public bool TryAdd(Guid messageId) => _seen.Add(messageId);
}
