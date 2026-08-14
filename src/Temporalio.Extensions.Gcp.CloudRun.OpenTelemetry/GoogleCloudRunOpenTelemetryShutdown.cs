using System;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using OpenTelemetryConfiguration = Temporalio.Extensions.OpenTelemetry.OpenTelemetryConfiguration;

namespace Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry
{
    /// <summary>
    /// Owns the tracer provider created by
    /// <see cref="TemporalClientConnectOptionsExtensions.ApplyGoogleCloudRunOpenTelemetryDefaults" />
    /// and flushes and releases it on shutdown.
    /// </summary>
    /// <remarks>
    /// The same handle may back multiple clients and workers. Call <see cref="FlushAsync" /> and/or
    /// <see cref="Dispose" /> only after every worker and client using the options has stopped.
    /// WARNING: Google Cloud Run support is experimental.
    /// </remarks>
    public sealed class GoogleCloudRunOpenTelemetryShutdown : IDisposable
    {
        private readonly TracerProvider tracerProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleCloudRunOpenTelemetryShutdown"/>
        /// class.
        /// </summary>
        /// <param name="tracerProvider">Tracer provider to own.</param>
        internal GoogleCloudRunOpenTelemetryShutdown(TracerProvider tracerProvider) =>
            this.tracerProvider = tracerProvider;

        /// <summary>
        /// Force-flush buffered traces without shutting down the provider.
        /// </summary>
        /// <param name="flushTimeout">Maximum time to wait for the flush.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task for the flush.</returns>
        /// <remarks>
        /// Core SDK metrics are exported periodically by the runtime and have no explicit flush API,
        /// so this flushes traces only. Call it during the graceful-shutdown window after stopping
        /// the worker.
        /// </remarks>
        public Task FlushAsync(TimeSpan flushTimeout, CancellationToken cancellationToken = default) =>
            OpenTelemetryConfiguration.ForceFlushAsync(
                this.tracerProvider, flushTimeout, cancellationToken);

        /// <summary>
        /// Dispose the owned tracer provider, flushing any remaining traces.
        /// </summary>
        public void Dispose()
        {
            this.tracerProvider.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
