# AWS Lambda Worker Support

This extension adds an AWS Lambda handler helper for running a Temporal worker during a Lambda invocation.

Add the `Temporalio.Extensions.Aws.Lambda` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Aws.Lambda). For example, using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.Aws.Lambda

## Quick Start

Create a static handler delegate and call it from the AWS Lambda handler method:

```csharp
using Amazon.Lambda.Core;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Extensions.Aws.Lambda;

public class Function
{
    private static readonly Func<object?, ILambdaContext, Task> WorkerHandler =
        TemporalLambdaWorker.CreateHandler(
            new WorkerDeploymentVersion("payments-worker", "2026-05-27"),
            config =>
            {
                config.ClientOptions = new TemporalClientConnectOptions
                {
                    TargetHost = "my-namespace.a1b2c.tmprl.cloud:7233",
                    Namespace = "my-namespace.a1b2c",
                    ApiKey = Environment.GetEnvironmentVariable("TEMPORAL_API_KEY"),
                    Tls = new TlsOptions(),
                };

                config.WorkerOptions.TaskQueue = "payments";
                config.WorkerOptions.AddWorkflow<PaymentWorkflow>();
                config.WorkerOptions.AddActivity(PaymentActivities.ChargeAsync);
            });

    public Task HandlerAsync(object? input, ILambdaContext context) =>
        WorkerHandler(input, context);
}
```

`configure` runs once when the delegate is created, which is normally during Lambda cold start. Each invocation creates a
fresh Temporal client and worker, runs until the Lambda deadline minus `ShutdownDeadlineBuffer`, then shuts down and runs
configured shutdown hooks.

## Configuration

Client configuration is explicit. This package does not load TOML profiles or environment-based client config for you:

```csharp
config.ClientOptions = new TemporalClientConnectOptions
{
    TargetHost = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS"),
    Namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default",
    ApiKey = Environment.GetEnvironmentVariable("TEMPORAL_API_KEY"),
    Tls = new TlsOptions(),
};
```

If `TEMPORAL_TASK_QUEUE` is present, it is used as the initial `WorkerOptions.TaskQueue`. You can still override the task
queue in `configure`. If no task queue is set by the environment or by `configure`, handler creation fails.

`TemporalLambdaWorker.CreateHandler` requires a `WorkerDeploymentVersion` and always enables Worker Versioning by setting
`WorkerOptions.DeploymentOptions` with `UseWorkerVersioning = true`. Use a deployment name and build ID that match your
rollout process. If you need to set the default versioning behavior, configure
`config.WorkerOptions.DeploymentOptions.DefaultVersioningBehavior`; the handler preserves that value while enforcing the
deployment version passed to `CreateHandler`.

The helper applies Lambda-oriented worker defaults before `configure`, including lower concurrency, a 5 second graceful
shutdown timeout, a smaller workflow cache, simple poller limits, and disabled eager activity execution. Values you set in
`configure` override these defaults except for the enforced deployment version.

## Shutdown Hooks

Add shutdown hooks for per-invocation cleanup:

```csharp
config.ShutdownHooks.Add(async cancellationToken =>
{
    await FlushTelemetryAsync(cancellationToken);
});
```

Hooks run in order after the worker has stopped. Hook failures are logged to the Lambda context logger and later hooks
still run.

## Observability

For tracing, add `Temporalio.Extensions.OpenTelemetry.TracingInterceptor` to `ClientOptions.Interceptors` and configure
your OpenTelemetry provider in the Lambda function:

```csharp
config.ClientOptions.Interceptors = new[] { new TracingInterceptor() };
```

For Core metrics, create a `TemporalRuntime` with `TelemetryOptions.Metrics.OpenTelemetry` and assign it to
`ClientOptions.Runtime`:

```csharp
config.ClientOptions.Runtime = new TemporalRuntime(new TemporalRuntimeOptions
{
    Telemetry = new TelemetryOptions
    {
        Metrics = new MetricsOptions(new OpenTelemetryOptions("http://collector:4317")),
    },
});
```

This package does not add OpenTelemetry SDK or exporter dependencies.

## TLS/CA Notes

Some AWS Lambda .NET images can override `SSL_CERT_FILE` in a way that prevents the SDK's Rust-based runtime from loading
system root CAs. See the SDK root README's
[AWS Lambda .NET CA loading workaround](https://github.com/temporalio/sdk-dotnet#aws-lambda-net-ca-loading-issues).
