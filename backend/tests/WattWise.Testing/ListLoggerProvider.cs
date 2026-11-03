using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace WattWise.Testing;

/// <summary>A logger provider that keeps every entry, so a test can assert on what a host or service logged.</summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Category, string Message)> entries = new();

    public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries => [.. entries];

    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(string category, ConcurrentQueue<(LogLevel, string, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, category, formatter(state, exception)));
    }
}
