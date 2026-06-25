using Microsoft.Extensions.Logging;

namespace TemperatureMonitor.Tests;

/// <summary>
/// Records log calls for assertion in tests.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public record LogEntry(LogLevel Level, string Message);

    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    public IEnumerable<LogEntry> Warnings => Entries.Where(e => e.Level == LogLevel.Warning);
    public IEnumerable<LogEntry> Debugs   => Entries.Where(e => e.Level == LogLevel.Debug);
}
