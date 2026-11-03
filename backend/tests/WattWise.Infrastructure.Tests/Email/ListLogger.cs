using Microsoft.Extensions.Logging;

namespace WattWise.Infrastructure.Tests.Email;

/// <summary>
/// A hand-written logger that keeps every entry: level, rendered message, the exception's text and the structured
/// fields as "Name=value" pairs, so a test can check everything an entry carries.
/// </summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, string Exception, string Fields)> Entries { get; } = [];

    public IEnumerable<(LogLevel Level, string Message)> LevelsAndMessages => Entries.Select(entry => (entry.Level, entry.Message));

    /// <summary>Everything every entry carries, as one string.</summary>
    public string Everything => string.Concat(Entries.Select(entry => $"{entry.Message}|{entry.Exception}|{entry.Fields}\n"));

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(';', pairs.Select(pair => $"{pair.Key}={pair.Value}"))
            : string.Empty;
        Entries.Add((logLevel, formatter(state, exception), exception?.ToString() ?? string.Empty, fields));
    }
}
