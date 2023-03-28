using System;
using Microsoft.Extensions.Logging;

namespace Temporalio.Worker
{
    /// <summary>
    /// Logger that will not log during replay.
    /// </summary>
    internal class ReplaySafeLogger : ILogger
    {
        private readonly ILogger underlying;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaySafeLogger"/> class.
        /// </summary>
        /// <param name="underlying">Logger to wrap.</param>
        public ReplaySafeLogger(ILogger underlying) => this.underlying = underlying;

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => underlying.BeginScope<TState>(state);

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => underlying.IsEnabled(logLevel);

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!Workflows.Workflow.Unsafe.IsReplaying)
            {
                underlying.Log<TState>(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}