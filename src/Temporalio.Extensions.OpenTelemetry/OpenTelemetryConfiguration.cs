using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Runtime;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Provider-neutral configuration shared by platform-specific OpenTelemetry extensions.
    /// </summary>
    internal static class OpenTelemetryConfiguration
    {
        /// <summary>
        /// OpenTelemetry service name environment variable.
        /// </summary>
        internal const string ServiceNameEnvironmentVariable = "OTEL_SERVICE_NAME";

        /// <summary>
        /// OpenTelemetry OTLP endpoint environment variable.
        /// </summary>
        internal const string OtlpEndpointEnvironmentVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

        /// <summary>
        /// Default endpoint for OTLP over gRPC.
        /// </summary>
        internal const string DefaultOtlpEndpoint = "http://localhost:4317";

        private const string ServiceNameResourceAttribute = "service.name";

        /// <summary>
        /// Resolves common options from provider-resolved values.
        /// </summary>
        /// <param name="collectorEndpoint">Collector endpoint.</param>
        /// <param name="serviceName">Service name.</param>
        /// <param name="metricsExportInterval">Metrics export interval.</param>
        /// <param name="optionsParameterName">Public options parameter name for validation errors.</param>
        /// <returns>Resolved OpenTelemetry options.</returns>
        internal static ResolvedOpenTelemetryOptions ResolveOptions(
            string collectorEndpoint,
            string serviceName,
            TimeSpan metricsExportInterval,
            string optionsParameterName)
        {
            if (metricsExportInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    optionsParameterName,
                    "MetricsExportInterval must be greater than zero");
            }

            return new ResolvedOpenTelemetryOptions(
                new Uri(collectorEndpoint),
                serviceName,
                metricsExportInterval);
        }

        /// <summary>
        /// Returns the first non-whitespace value, or null if no such value exists.
        /// </summary>
        /// <param name="values">Candidate values in priority order.</param>
        /// <returns>The first non-whitespace value, or null.</returns>
        internal static string? FirstNonWhitespaceOrDefault(IEnumerable<string?> values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        /// <summary>
        /// Applies the standard Temporal runtime and tracing interceptor configuration.
        /// </summary>
        /// <param name="clientOptions">Client options to mutate.</param>
        /// <param name="options">Resolved OpenTelemetry options.</param>
        internal static void ApplyToClientOptions(
            TemporalClientConnectOptions clientOptions,
            ResolvedOpenTelemetryOptions options)
        {
            clientOptions.Interceptors = AddTracingInterceptor(clientOptions.Interceptors);
            clientOptions.Runtime = CreateRuntime(options);
        }

        /// <summary>
        /// Force-flushes a tracer provider without blocking the caller's thread.
        /// </summary>
        /// <param name="forceFlush">
        /// Function accepting a timeout in milliseconds and returning whether the flush completed
        /// within that timeout.
        /// </param>
        /// <param name="flushTimeout">Maximum time for the provider flush.</param>
        /// <param name="cancellationToken">Cancellation token for waiting on the flush.</param>
        /// <returns>A task for the flush.</returns>
        internal static async Task ForceFlushAsync(
            Func<int, bool> forceFlush,
            TimeSpan flushTimeout,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var flushTask = Task.Run(
                () => forceFlush(ToTimeoutMilliseconds(flushTimeout)));
            try
            {
                await flushTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                flushTask.Forget();
            }
        }

        private static List<IClientInterceptor> AddTracingInterceptor(
            IReadOnlyCollection<IClientInterceptor>? interceptors)
        {
            var newInterceptors = interceptors?.ToList() ?? new List<IClientInterceptor>();
            newInterceptors.Add(new TracingInterceptor());
            return newInterceptors;
        }

        private static TemporalRuntime CreateRuntime(ResolvedOpenTelemetryOptions options)
        {
            var openTelemetryOptions = new Temporalio.Runtime.OpenTelemetryOptions(
                options.CollectorEndpoint)
            {
                MetricsExportInterval = options.MetricsExportInterval,
                Protocol = OpenTelemetryProtocol.Grpc,
            };
            return new TemporalRuntime(new TemporalRuntimeOptions
            {
                Telemetry = new TelemetryOptions
                {
                    Metrics = new MetricsOptions(openTelemetryOptions)
                    {
                        GlobalTags = new[]
                        {
                            new KeyValuePair<string, string>(
                                ServiceNameResourceAttribute,
                                options.ServiceName),
                        },
                    },
                },
            });
        }

        private static int ToTimeoutMilliseconds(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                return 0;
            }
            if (timeout.TotalMilliseconds >= int.MaxValue)
            {
                return int.MaxValue;
            }
            return (int)timeout.TotalMilliseconds;
        }
    }
}
