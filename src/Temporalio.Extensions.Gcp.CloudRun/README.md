# Google Cloud Run Worker Support

This extension derives a Temporal worker identity and a `WorkerDeploymentVersion` from Google Cloud
Run instance metadata, for use with a normal long-lived worker on Cloud Run worker pools and
services.

Add the `Temporalio.Extensions.Gcp.CloudRun` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Gcp.CloudRun). For example, using the
`dotnet` CLI:

    dotnet add package Temporalio.Extensions.Gcp.CloudRun

## Quick Start

Apply the Cloud Run defaults to your client and worker options, then run a normal long-lived worker.
`ApplyGoogleCloudRunDefaultsAsync` fetches the metadata once and sets the client identity;
`ApplyGoogleCloudRunDefaults` forces the worker deployment version:

```csharp
using System;
using System.Threading;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun;
using Temporalio.Worker;

var connectOptions = new TemporalClientConnectOptions("my-namespace.a1b2c.tmprl.cloud:7233")
{
    Namespace = "my-namespace",
    // ... Temporal Cloud API key / mTLS credentials ...
};

// Fetch Cloud Run metadata once and set the client identity from it (only if not already set).
var metadata = await connectOptions.ApplyGoogleCloudRunDefaultsAsync();

var client = await TemporalClient.ConnectAsync(connectOptions);

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions("my-task-queue").
        ApplyGoogleCloudRunDefaults(metadata).
        AddWorkflow<MyWorkflow>().
        AddActivity(MyActivities.DoThing));

// Cloud Run sends SIGTERM before stopping the instance; cancel the worker on it.
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

try
{
    await worker.ExecuteAsync(shutdown.Token);
}
catch (OperationCanceledException)
{
    // Expected shutdown path.
}
```

If you need the raw values, call `GoogleCloudRunMetadata.FetchAsync()` directly and read
`WorkerIdentity` / `ToWorkerDeploymentVersion()` yourself.

## How it works

Unlike AWS Lambda, Cloud Run runs a long-lived container with no per-invocation handler to wrap, so
this is a small metadata helper rather than a worker wrapper. You apply its results to the client
and worker options you already build.

`GoogleCloudRunMetadata.FetchAsync` gathers three values:

* `Name` (the Temporal deployment name) from the `CLOUD_RUN_WORKER_POOL` environment variable, then
  the `K_SERVICE` environment variable.
* `Revision` from the `CLOUD_RUN_REVISION` environment variable, then the `K_REVISION` environment
  variable.
* `InstanceId` from the Cloud Run metadata server
  (`http://metadata.google.internal/computeMetadata/v1/instance/id`), read with the required
  `Metadata-Flavor: Google` request header. This is the only value not available as an environment
  variable, so the helper makes a single HTTP GET at startup.

Cloud Run worker pools receive the `CLOUD_RUN_*` variables (and no `K_*` variables), while Cloud Run
services receive the `K_*` variables. The metadata server is available on both, so resolving the
name and revision in that order covers both deployment types. Worker pools are the primary target.

From those values:

* `WorkerIdentity` is `{InstanceId}@{Revision}`, falling back to `{InstanceId}@{Name}` when the
  revision is empty, or just `{InstanceId}` when both are empty.
  `TemporalClientConnectOptions.ApplyGoogleCloudRunDefaultsAsync` assigns it to
  `TemporalClientConnectOptions.Identity`, but only when the identity is not already set, so an
  explicitly configured identity wins.
* `ToWorkerDeploymentVersion()` returns a `WorkerDeploymentVersion` whose deployment name is the
  Cloud Run name and whose build id is the Cloud Run revision.
  `TemporalWorkerOptions.ApplyGoogleCloudRunDefaults` sets it on
  `TemporalWorkerOptions.DeploymentOptions` with `useWorkerVersioning: true` and a
  `VersioningBehavior.Pinned` default (a per-workflow behavior takes precedence), so each Cloud Run
  revision maps to a worker deployment version. It throws if the name or revision is empty, which
  usually means the process is not running on Cloud Run.

`FetchAsync` also throws a clear `InvalidOperationException` if the metadata server cannot be
reached, which likewise usually means the process is not running on Cloud Run.
