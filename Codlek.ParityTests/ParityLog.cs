using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CodlekWeb.Tests;

/// <summary>
/// Writes the new API's warnings and errors to a file while the parity
/// suite runs — <b>only when <c>PARITY_LOG</c> names a file</b>.
///
/// <para>A failing legacy assertion says "Rejected" but not why; the
/// server already logs the reason (orphan report, capability denied,
/// duplicate code…). This puts those lines next to the failures.</para>
/// </summary>
public sealed class ParityLog : ILoggerProvider
{
    public static readonly string? Path = Environment.GetEnvironmentVariable("PARITY_LOG");

    private static readonly object Gate = new();

    private readonly ConcurrentDictionary<string, Logger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new Logger(name));

    public void Dispose() { }

    private sealed class Logger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => Path is not null && logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string line = $"{DateTime.UtcNow:HH:mm:ss.fff} {logLevel} {category}: {formatter(state, exception)}"
                + (exception is null ? "" : " | " + exception.GetType().Name + ": " + exception.Message);

            lock (Gate) File.AppendAllText(Path!, line + Environment.NewLine);
        }
    }
}
