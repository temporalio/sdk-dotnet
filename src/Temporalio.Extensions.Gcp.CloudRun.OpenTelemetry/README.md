# Google Cloud Run Worker OpenTelemetry Support

This extension adds OpenTelemetry defaults for Temporal workers running on Google Cloud Run,
primarily in [worker pools](https://cloud.google.com/run/docs/deploying-worker-pools). Temporal
metrics and traces are exported over OTLP/gRPC to a local
[OpenTelemetry Collector](https://opentelemetry.io/docs/collector/) sidecar; the collector is
responsible for Google Cloud resource detection, authentication, and export.

Add the `Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry). For
example, using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry

## Quick Start

Call `ApplyGoogleCloudRunOpenTelemetryDefaults` on the client connect options before connecting,
and dispose the returned handle after the worker stops:

```csharp
using System.Diagnostics;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Temporalio.Worker;

var connectOptions = new TemporalClientConnectOptions("my-namespace.a1b2c.tmprl.cloud:7233")
{
    Namespace = "my-namespace",
    // ... Temporal Cloud API key / mTLS credentials ...
};

// Applies tracing + metrics defaults and returns a handle owning the tracer provider.
using var telemetry = connectOptions.ApplyGoogleCloudRunOpenTelemetryDefaults();

var client = await TemporalClient.ConnectAsync(connectOptions);

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions("my-task-queue").
        AddWorkflow<MyWorkflow>().
        AddActivity(MyActivities.DoThing));

// Cloud Run allows 10 seconds from SIGTERM before terminating the instance. Track how much of
// that window worker shutdown consumes so trace flushing uses only the time that remains.
var shutdownStarted = new TaskCompletionSource<Stopwatch>(
    TaskCreationOptions.RunContinuationsAsynchronously);
using var registration = shutdownToken.Register(
    () => shutdownStarted.TrySetResult(Stopwatch.StartNew()));

try
{
    await worker.ExecuteAsync(shutdownToken);
}
catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
{
    // SIGTERM cancellation is the expected shutdown path.
}
finally
{
    var elapsed = shutdownToken.IsCancellationRequested ?
        (await shutdownStarted.Task).Elapsed :
        TimeSpan.Zero;
    var flushTimeout = TimeSpan.FromSeconds(10) - elapsed;
    if (flushTimeout > TimeSpan.Zero)
    {
        await telemetry.FlushAsync(flushTimeout);
    }
}
```

`ApplyGoogleCloudRunOpenTelemetryDefaults`:

* Adds the Temporal `TracingInterceptor` to the client interceptors (it applies to workers created
  from the client too).
* Creates an OTLP gRPC trace exporter and tracer provider registered against the four
  `TracingInterceptor` activity sources.
* Configures Core SDK metrics through a `TemporalRuntime` and sets it on the connect options,
  replacing any runtime already set.

## Defaults

The OTLP collector endpoint is resolved from `GoogleCloudRunOpenTelemetryOptions.CollectorEndpoint`,
then `OTEL_EXPORTER_OTLP_ENDPOINT`, then `http://localhost:4317`.

The OpenTelemetry service name is resolved from `GoogleCloudRunOpenTelemetryOptions.ServiceName`,
then `OTEL_SERVICE_NAME`, then `CLOUD_RUN_WORKER_POOL`, then `K_SERVICE`, then `temporal-worker`.

Core SDK metrics export every 60 seconds by default. This matches the coordinated Temporal SDK
Google Cloud Run default and the upstream OpenTelemetry SDK default, and is above Google Cloud's
five-second minimum export interval. Override it with `MetricsExportInterval`:

```csharp
using var telemetry = connectOptions.ApplyGoogleCloudRunOpenTelemetryDefaults(
    new GoogleCloudRunOpenTelemetryOptions
    {
        CollectorEndpoint = "http://localhost:4317",
        ServiceName = "payments-worker",
        MetricsExportInterval = TimeSpan.FromSeconds(60),
    });
```

## Collector Sidecar

Run the [Google-Built OpenTelemetry Collector](https://cloud.google.com/stackdriver/docs/instrumentation/opentelemetry-collector-cloud-run)
as a sidecar in the Cloud Run service, listening on `localhost:4317`.

Export cumulative metrics directly to Google Managed Service for Prometheus **without** a collector
batch processor: a batch processor can combine a periodic export with a shutdown-time export into a
single Google Monitoring request and cause a `Duplicate TimeSeries` rejection. Traces can and should
keep a dedicated batch processor.

## Shutdown

Cloud Run workers are long-running, so this extension does not flush automatically. On shutdown,
stop the worker first, catch the expected cancellation from `ExecuteAsync`, then call `FlushAsync`
from a `finally` block within the remaining termination grace period. Dispose the handle afterward
(disposing also flushes). Core SDK metrics have no explicit flush API and are exported periodically
by the runtime.
