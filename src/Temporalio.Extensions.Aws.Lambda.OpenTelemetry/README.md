# AWS Lambda Worker OpenTelemetry Support

This extension adds OpenTelemetry helpers for Temporal workers running inside AWS Lambda.

Add the `Temporalio.Extensions.Aws.Lambda.OpenTelemetry` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Aws.Lambda.OpenTelemetry). For example, using the
`dotnet` CLI:

    dotnet add package Temporalio.Extensions.Aws.Lambda.OpenTelemetry

## Quick Start

Call `LambdaWorkerOpenTelemetry.ApplyDefaults` from the Lambda worker configure callback:

```csharp
using Amazon.Lambda.Core;
using Temporalio.Common;
using Temporalio.Extensions.Aws.Lambda;
using Temporalio.Extensions.Aws.Lambda.OpenTelemetry;

private static readonly Func<object?, ILambdaContext, Task> WorkerHandler =
    TemporalLambdaWorker.CreateHandler(
        new WorkerDeploymentVersion("payments-worker", "2026-05-27"),
        config =>
        {
            LambdaWorkerOpenTelemetry.ApplyDefaults(config);

            config.WorkerOptions.TaskQueue = "payments";
            config.WorkerOptions.AddWorkflow<PaymentWorkflow>();
            config.WorkerOptions.AddActivity(PaymentActivities.ChargeAsync);
        });
```

`ApplyDefaults` configures Temporal tracing with `TracingInterceptor`, creates an OTLP trace exporter and tracer
provider, configures Core SDK metrics through a `TemporalRuntime`, and registers a per-invocation shutdown hook that
force-flushes traces before the Lambda invocation ends.
It replaces any runtime already set on `config.ClientOptions`.

## Defaults

The OTLP collector endpoint is resolved from `LambdaWorkerOpenTelemetryOptions.CollectorEndpoint`, then
`OTEL_EXPORTER_OTLP_ENDPOINT`, then `http://localhost:4317`, which is the endpoint expected by the AWS Distro for
OpenTelemetry collector Lambda layer.

The OpenTelemetry service name is resolved from `LambdaWorkerOpenTelemetryOptions.ServiceName`, then
`OTEL_SERVICE_NAME`, then `AWS_LAMBDA_FUNCTION_NAME`, then `temporal-lambda-worker`.

Core SDK metrics export every 10 seconds by default. Set `MetricsExportInterval` shorter than your Lambda timeout to
increase the chance that at least one metrics export happens during each invocation. Core metrics are exported
periodically and do not have an explicit per-invocation flush API.

```csharp
LambdaWorkerOpenTelemetry.ApplyDefaults(
    config,
    new LambdaWorkerOpenTelemetryOptions
    {
        CollectorEndpoint = "http://localhost:4317",
        ServiceName = "payments-worker",
        MetricsExportInterval = TimeSpan.FromSeconds(5),
    });
```

## AWS Lambda Setup

Attach the ADOT collector Lambda layer and set:

```text
OPENTELEMETRY_COLLECTOR_CONFIG_URI=/var/task/otel-collector-config.yaml
```

The helper uses AWS X-Ray-compatible trace IDs. It force-flushes traces on every Lambda invocation and disposes the
tracer provider during the Lambda shutdown hook.
