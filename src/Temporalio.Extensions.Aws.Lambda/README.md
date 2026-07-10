# AWS Lambda Worker Support

This extension adds an AWS Lambda handler helper for running a Temporal worker during a Lambda invocation.

Add the `Temporalio.Extensions.Aws.Lambda` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Aws.Lambda). For example, using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.Aws.Lambda

## Quick Start

Create a static handler delegate and call it from the AWS Lambda handler method:

```csharp
using Amazon.Lambda.Core;
using Temporalio.Common;
using Temporalio.Extensions.Aws.Lambda;

public class Function
{
    private static readonly Func<object?, ILambdaContext, Task> WorkerHandler =
        TemporalLambdaWorker.CreateHandler(
            new WorkerDeploymentVersion("payments-worker", "2026-05-27"),
            options =>
            {
                options.WorkerOptions.TaskQueue = "payments";
                options.WorkerOptions.AddWorkflow<PaymentWorkflow>();
                options.WorkerOptions.AddActivity(PaymentActivities.ChargeAsync);
            });

    public Task HandlerAsync(object? input, ILambdaContext context) =>
        WorkerHandler(input, context);
}
```

The synchronous `configure` callback shown above runs once when the delegate is created, which is normally during Lambda
cold start. Use it for static worker setup such as task queues, workflow/activity registration, and options that can be
shared across warm invocations. Each invocation creates a fresh Temporal client and worker, runs until the Lambda
deadline minus `ShutdownDeadlineBuffer`, then shuts down and runs configured shutdown hooks.

For setup that must be awaited per invocation, use the async overload:

```csharp
private static readonly Func<object?, ILambdaContext, Task> WorkerHandler =
    TemporalLambdaWorker.CreateHandler(
        new WorkerDeploymentVersion("payments-worker", "2026-05-27"),
        async options =>
        {
            options.WorkerOptions.TaskQueue = "payments";
            options.WorkerOptions.AddWorkflow<PaymentWorkflow>();
            options.WorkerOptions.AddActivity(PaymentActivities.ChargeAsync);

            options.ClientOptions.ApiKey = await LoadTemporalApiKeyAsync();
            options.AddShutdownHook(async cancellationToken =>
            {
                await FlushPerInvocationResourceAsync(cancellationToken);
            });
        });
```

The async `configure` callback is awaited once per Lambda invocation, before the Temporal client connects. It receives a
fresh `TemporalLambdaWorkerOptions` each time, so use shutdown hooks for any per-invocation cleanup.

## Configuration

Client connection settings are pre-populated from a `temporal.toml` file and/or environment variables via
`Temporalio.Common.EnvConfig`. The Lambda config file path is selected as follows:

1. `TEMPORAL_CONFIG_FILE`, if set.
2. Otherwise, `temporal.toml` in `$LAMBDA_TASK_ROOT`, typically `/var/task`, when `LAMBDA_TASK_ROOT` is set.
3. Otherwise, `temporal.toml` in the current working directory.

The file is optional. If it does not exist, only environment variables are used.

To bypass config loading, assign explicit client options in `configure`:

```csharp
options.ClientOptions = new TemporalClientConnectOptions
{
    TargetHost = "my-namespace.a1b2c.tmprl.cloud:7233",
    Namespace = "my-namespace.a1b2c",
    ApiKey = Environment.GetEnvironmentVariable("TEMPORAL_API_KEY"),
    Tls = new TlsOptions(),
};
```

If `TEMPORAL_TASK_QUEUE` is present, it is used as the initial `WorkerOptions.TaskQueue`. You can still override the task
queue in `configure`. If no task queue is set by the environment or by `configure`, synchronous handler creation fails;
with async configure, the invocation fails after the callback is awaited.

`TemporalLambdaWorker.CreateHandler` requires a `WorkerDeploymentVersion` and always enables Worker Versioning by setting
`WorkerOptions.DeploymentOptions` with `UseWorkerVersioning = true`. Use a deployment name and build ID that match your
rollout process. The default versioning behavior is `AutoUpgrade`. If you need a different default versioning behavior,
configure `options.WorkerOptions.DeploymentOptions.DefaultVersioningBehavior`; the handler preserves any non-`Unspecified`
value while enforcing the deployment version passed to `CreateHandler`.

The helper applies Lambda-oriented worker defaults before `configure`, including lower concurrency, a 5 second graceful
shutdown timeout, a smaller workflow cache, simple poller limits, and disabled eager activity execution. Values you set in
`configure` override these defaults except for the enforced deployment version. With sync configure this happens once at
handler creation; with async configure it happens for every invocation.

## Shutdown Hooks

The worker runs until the Lambda remaining time reaches `ShutdownDeadlineBuffer`, then begins shutdown. Increase the
buffer if your worker needs more time to stop polling, finish shutdown hooks, or flush telemetry before the Lambda
deadline. Set it in `configure`:

```csharp
options.ShutdownDeadlineBuffer = TimeSpan.FromSeconds(15);
```

Add shutdown hooks for per-invocation cleanup:

```csharp
options.AddShutdownHook(async cancellationToken =>
{
    await FlushTelemetryAsync(cancellationToken);
});
```

Hooks run in order after the worker has stopped. Hook failures are logged to the Lambda context logger and later hooks
still run. Hooks added from async configure are scoped to that invocation.

## Observability

For AWS Lambda OpenTelemetry defaults, add the `Temporalio.Extensions.Aws.Lambda.OpenTelemetry` package and call
`ApplyOpenTelemetryDefaults` in `configure`:

```csharp
using Temporalio.Extensions.Aws.Lambda.OpenTelemetry;

options.ApplyOpenTelemetryDefaults();
```

This configures Temporal tracing, Core SDK OTLP metrics, AWS X-Ray-compatible trace IDs, and a per-invocation trace
flush shutdown hook. The OTLP endpoint defaults to `OTEL_EXPORTER_OTLP_ENDPOINT`, then `http://localhost:4317`, which is
the endpoint expected by the ADOT collector Lambda layer.

You can still configure tracing and metrics manually using `Temporalio.Extensions.OpenTelemetry.TracingInterceptor` and
`TemporalRuntime`:

```csharp
options.ClientOptions.Interceptors = new[] { new TracingInterceptor() };
options.ClientOptions.Runtime = new TemporalRuntime(new TemporalRuntimeOptions
{
    Telemetry = new TelemetryOptions
    {
        Metrics = new MetricsOptions(new OpenTelemetryOptions("http://collector:4317")),
    },
});
```

## TLS/CA Notes

Some AWS Lambda .NET images can override `SSL_CERT_FILE` in a way that prevents the SDK's Rust-based runtime from loading
system root CAs. See the SDK root README's
[AWS Lambda .NET CA loading workaround](https://github.com/temporalio/sdk-dotnet#aws-lambda-net-ca-loading-issues).
