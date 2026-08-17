using System;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Creates tracer providers for platform extensions that depend on the full OpenTelemetry SDK.
    /// </summary>
    internal static class OpenTelemetryTracerProviderFactory
    {
        /// <summary>
        /// Creates the standard OTLP tracer provider, optionally customized by a platform wrapper.
        /// </summary>
        /// <param name="options">Resolved OpenTelemetry options.</param>
        /// <param name="configureBuilder">Optional platform-specific builder customization.</param>
        /// <returns>The created tracer provider.</returns>
        internal static TracerProvider CreateTracerProvider(
            ResolvedOpenTelemetryOptions options,
            Action<TracerProviderBuilder>? configureBuilder = null)
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            configureBuilder?.Invoke(builder);
            return builder.
                SetResourceBuilder(
                    ResourceBuilder.CreateDefault().AddService(options.ServiceName)).
                AddSource(
                    TracingInterceptor.ClientSource.Name,
                    TracingInterceptor.WorkflowsSource.Name,
                    TracingInterceptor.ActivitiesSource.Name,
                    TracingInterceptor.NexusSource.Name).
                AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = options.CollectorEndpoint;
#pragma warning disable CS0618 // Provider extensions use OTLP gRPC collectors on localhost:4317.
                    exporterOptions.Protocol = OtlpExportProtocol.Grpc;
#pragma warning restore CS0618
                }).
                Build();
        }
    }
}
