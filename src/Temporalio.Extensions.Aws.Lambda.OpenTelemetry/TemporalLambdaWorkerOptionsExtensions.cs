using System;
using OpenTelemetry.Trace;
using OpenTelemetryConfiguration = Temporalio.Extensions.OpenTelemetry.OpenTelemetryConfiguration;
using ResolvedOpenTelemetryOptions = Temporalio.Extensions.OpenTelemetry.ResolvedOpenTelemetryOptions;

namespace Temporalio.Extensions.Aws.Lambda.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry extensions for <see cref="TemporalLambdaWorkerOptions" /> for Temporal
    /// workers running inside AWS Lambda.
    /// </summary>
    /// <remarks>WARNING: AWS Lambda support is experimental.</remarks>
    public static class TemporalLambdaWorkerOptionsExtensions
    {
        private const string DefaultCollectorEndpoint = "http://localhost:4317";
        private const string DefaultServiceName = "temporal-lambda-worker";
        private const string LambdaFunctionNameEnvironmentVariable = "AWS_LAMBDA_FUNCTION_NAME";

        /// <summary>
        /// Configure OpenTelemetry metrics and tracing with AWS Lambda defaults.
        /// </summary>
        /// <param name="options">Lambda worker configuration to mutate.</param>
        /// <param name="openTelemetryOptions">Optional OpenTelemetry configuration.</param>
        /// <remarks>
        /// This creates an OTLP trace exporter and tracer provider, configures Core SDK metrics
        /// through a Temporal runtime, adds the Temporal tracing interceptor, and registers a
        /// per-invocation shutdown hook to force-flush traces and dispose the tracer provider
        /// before the Lambda invocation ends.
        /// Any existing <see cref="Temporalio.Client.TemporalConnectionOptions.Runtime" /> is
        /// replaced.
        /// </remarks>
        public static void ApplyOpenTelemetryDefaults(
            this TemporalLambdaWorkerOptions options,
            LambdaWorkerOpenTelemetryOptions? openTelemetryOptions = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var resolvedOptions = ResolveOptions(openTelemetryOptions);
#pragma warning disable CA2000 // The per-invocation shutdown hook owns provider disposal.
            var tracerProvider = OpenTelemetryConfiguration.
                CreateTracerProvider(resolvedOptions, builder => builder.AddXRayTraceId());
#pragma warning restore CA2000

            OpenTelemetryConfiguration.ApplyToClientOptions(
                options.ClientOptions,
                resolvedOptions);
            options.AddShutdownHook(
                async cancellationToken =>
                {
                    // CreateHandler runs configuration once per invocation, so this provider is
                    // invocation-scoped rather than warm-container-scoped. ForceFlush is the only
                    // bounded part of provider shutdown: Dispose is synchronous and has no
                    // cancellation-aware API. Run disposal after the flush attempt so exporting gets
                    // first use of the remaining Lambda deadline, while still releasing provider
                    // resources before the next warm invocation can accumulate another provider.
                    try
                    {
                        await OpenTelemetryConfiguration.ForceFlushAsync(
                            tracerProvider,
                            options.ShutdownDeadlineBuffer,
                            cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        tracerProvider.Dispose();
                    }
                });
        }

        /// <summary>
        /// Resolve options using process environment variables.
        /// </summary>
        /// <param name="options">Options to resolve.</param>
        /// <returns>Resolved options.</returns>
        internal static ResolvedOpenTelemetryOptions ResolveOptions(
            LambdaWorkerOpenTelemetryOptions? options = null)
        {
            options ??= new LambdaWorkerOpenTelemetryOptions();
            return OpenTelemetryConfiguration.ResolveOptions(
                new string?[]
                {
                    options.CollectorEndpoint,
                    Environment.GetEnvironmentVariable(
                        OpenTelemetryConfiguration.OtlpEndpointEnvironmentVariable),
                    DefaultCollectorEndpoint,
                },
                new string?[]
                {
                    options.ServiceName,
                    Environment.GetEnvironmentVariable(
                        OpenTelemetryConfiguration.ServiceNameEnvironmentVariable),
                    Environment.GetEnvironmentVariable(LambdaFunctionNameEnvironmentVariable),
                    DefaultServiceName,
                },
                options.MetricsExportInterval,
                nameof(options));
        }
    }
}
