namespace Temporalio.Tests;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public static class TestUtils
{
    public static string CallerFilePath(
        [System.Runtime.CompilerServices.CallerFilePath] string? callerPath = null)
    {
        return callerPath ?? throw new ArgumentException("Unable to find caller path");
    }

    public record LogEntry(
        LogLevel Level,
        EventId EventID,
        object? State,
        Exception? Exception,
        string Formatted);

    public sealed class LogCaptureFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<LogEntry> logs = new();
        private readonly ILoggerFactory underlying;

        public LogCaptureFactory(ILoggerFactory underlying) => this.underlying = underlying;

        public IReadOnlyCollection<LogEntry> Logs => logs;

        public void ClearLogs() => logs.Clear();

        public void AddProvider(ILoggerProvider provider) => underlying.AddProvider(provider);

        public ILogger CreateLogger(string categoryName) =>
            new LogCaptureLogger(underlying.CreateLogger(categoryName), logs);

        public void Dispose() => underlying.Dispose();
    }

    public class LogCaptureLogger : ILogger
    {
        private readonly ILogger underlying;
        private readonly ConcurrentQueue<LogEntry> logs;

        internal LogCaptureLogger(ILogger underlying, ConcurrentQueue<LogEntry> logs)
        {
            this.underlying = underlying;
            this.logs = logs;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => underlying.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => underlying.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logs.Enqueue(new(logLevel, eventId, state, exception, formatter.Invoke(state, exception)));
            underlying.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}