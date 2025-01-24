namespace Temporalio.Tests;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Runtime;

public static class TestUtils
{
    public static string ReadAllFileText(
        string relativePath, [CallerFilePath] string sourceFilePath = "") =>
        File.ReadAllText(Path.Join(sourceFilePath, "..", relativePath));

    public static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public static async Task DeleteAllSchedulesAsync(ITemporalClient client)
    {
        // We will try this 3 times
        var tries = 0;
        while (true)
        {
            await foreach (var sched in client.ListSchedulesAsync())
            {
                try
                {
                    await client.GetScheduleHandle(sched.Id).DeleteAsync();
                }
                catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)
                {
                    // Ignore not-found errors
                }
            }
            try
            {
                await AssertNoSchedulesAsync(client);
                return;
            }
            catch
            {
                if (++tries >= 3)
                {
                    throw;
                }
            }
        }
    }

    public static async Task AssertNoSchedulesAsync(ITemporalClient client)
    {
        await AssertMore.EqualEventuallyAsync(
            0,
            async () =>
            {
                var count = 0;
                await foreach (var sched in client.ListSchedulesAsync())
                {
                    count++;
                }
                return count;
            });
    }

    public record LogEntry(
        LogLevel Level,
        EventId EventId,
        object? State,
        Exception? Exception,
        string Formatted,
        Dictionary<string, object?> ScopeValues);

    public sealed class LogCaptureFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<LogEntry> logs = new();
        private readonly ILoggerFactory underlying;
        private readonly IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

        public LogCaptureFactory(ILoggerFactory underlying) => this.underlying = underlying;

        public IReadOnlyCollection<LogEntry> Logs => logs;

        public void ClearLogs() => logs.Clear();

        public void AddProvider(ILoggerProvider provider) => underlying.AddProvider(provider);

        public ILogger CreateLogger(string categoryName) =>
            new LogCaptureLogger(underlying.CreateLogger(categoryName), logs, scopeProvider);

        public void Dispose()
        {
            // Do not dispose underlying log factory, it was created externally
        }
    }

    public class LogCaptureLogger : ILogger
    {
        private readonly ILogger underlying;
        private readonly ConcurrentQueue<LogEntry> logs;
        private readonly IExternalScopeProvider scopeProvider;

        internal LogCaptureLogger(
            ILogger underlying,
            ConcurrentQueue<LogEntry> logs,
            IExternalScopeProvider scopeProvider)
        {
            this.underlying = underlying;
            this.logs = logs;
            this.scopeProvider = scopeProvider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            new CompositeDisposable(scopeProvider.Push(state), underlying.BeginScope(state)!);

        public bool IsEnabled(LogLevel logLevel) => underlying.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Collect scopes
            var allScopeValues = new Dictionary<string, object?>();
            scopeProvider.ForEachScope(
                (state, allScopeValues) =>
                {
                    if (state is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                    {
                        foreach (var kv in scopeValues)
                        {
                            allScopeValues[kv.Key] = kv.Value;
                        }
                    }
                },
                allScopeValues);
            logs.Enqueue(new(
                logLevel,
                eventId,
                state,
                exception,
                formatter.Invoke(state, exception),
                allScopeValues));
            underlying.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    private record CompositeDisposable(params IDisposable[] Disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var d in Disposables)
            {
                d.Dispose();
            }
        }
    }

    public class CaptureMetricMeter : ICustomMetricMeter
    {
        public ConcurrentQueue<CaptureMetric> Metrics { get; } = new();

        public ICustomMetricCounter<T> CreateCounter<T>(
            string name, string? unit, string? description)
            where T : struct =>
            CreateMetric<T, ICustomMetricCounter<T>>(name, unit, description);

        public ICustomMetricHistogram<T> CreateHistogram<T>(
            string name, string? unit, string? description)
            where T : struct =>
            CreateMetric<T, ICustomMetricHistogram<T>>(name, unit, description);

        public ICustomMetricGauge<T> CreateGauge<T>(
            string name, string? unit, string? description)
            where T : struct =>
            CreateMetric<T, ICustomMetricGauge<T>>(name, unit, description);

        public object CreateTags(
            object? appendFrom, IReadOnlyCollection<KeyValuePair<string, object>> tags)
        {
            var dict = appendFrom == null ?
                new Dictionary<string, object>(tags.Count) :
                new Dictionary<string, object>((Dictionary<string, object>)appendFrom);
            foreach (var kv in tags)
            {
                dict[kv.Key] = kv.Value;
            }
            return dict;
        }

        private TMetric CreateMetric<TValue, TMetric>(string name, string? unit, string? description)
            where TValue : struct
        {
            var metric = new CaptureMetric<TValue>(name, unit, description);
            Metrics.Enqueue(metric);
            return (TMetric)(object)metric;
        }
    }

    public abstract record CaptureMetric(string Name, string? Unit, string? Description)
    {
        public ConcurrentQueue<(object Value, Dictionary<string, object> Tags)> Values { get; } = new();
    }

    public record CaptureMetric<T>(string Name, string? Unit, string? Description)
        : CaptureMetric(Name, Unit, Description), ICustomMetricCounter<T>, ICustomMetricHistogram<T>, ICustomMetricGauge<T>
        where T : struct
    {
        public void Add(T value, object tags) => Values.Enqueue((value, (Dictionary<string, object>)tags));

        public void Record(T value, object tags) => Values.Enqueue((value, (Dictionary<string, object>)tags));

        public void Set(T value, object tags) => Values.Enqueue((value, (Dictionary<string, object>)tags));
    }
}