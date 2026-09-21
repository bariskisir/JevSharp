using System.Collections.Concurrent;
using JevSharp.Core.Clients;
using Microsoft.Extensions.Logging;

namespace JevSharp.Tests.Support;

/// <summary>Captures structured diagnostic messages without requiring a logging provider.</summary>
internal sealed class CaptureLogger : ILogger<JevClient>
{
    internal ConcurrentQueue<string> Messages { get; } = new();

    /// <summary>Returns no scope resource.</summary>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    /// <summary>Enables all log levels for assertions.</summary>
    public bool IsEnabled(LogLevel logLevel) => true;
    /// <summary>Captures the formatted log message.</summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Enqueue(formatter(state, exception));
    }
}
