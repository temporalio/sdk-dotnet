# Google Cloud Run Worker Support

This extension provides `WorkerIdPlugin`, a Temporal client/worker plugin that derives a worker
identity and a `WorkerDeploymentVersion` from Google Cloud Run instance metadata, for use with a
normal long-lived worker on Cloud Run worker pools and services.

Add the `Temporalio.Extensions.Gcp.CloudRun` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Gcp.CloudRun). For example, using the
`dotnet` CLI:

    dotnet add package Temporalio.Extensions.Gcp.CloudRun

## Quick Start

Construct a `WorkerIdPlugin`, register it on your client connect options via `Plugins`, then run a
normal long-lived worker. Registering it once on the client is enough: the plugin sets the client
identity at connect time and, because it is also a worker plugin, automatically pins the worker to
the Cloud Run deployment version when the worker is created.

```csharp
using System;
using System.Threading;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun;
using Temporalio.Worker;

var connectOptions = new TemporalClientConnectOptions("my-namespace.a1b2c.tmprl.cloud:7233")
{
    Namespace = "my-namespace",
    // Register the plugin once on the client. It reads the Cloud Run metadata at connect time and
    // propagates to workers created from the connected client.
    Plugins = new[] { new WorkerIdPlugin() },
    // ... Temporal Cloud API key / mTLS credentials ...
};

var client = await TemporalClient.ConnectAsync(connectOptions);

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions("my-task-queue").
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

If you need the raw values instead, call `GoogleCloudRunMetadata.FetchAsync()` directly and read
`WorkerIdentity` / `ToWorkerDeploymentVersion()` yourself.

## How it works

Unlike AWS Lambda, Cloud Run runs a long-lived container with no per-invocation handler to wrap, so
this is a metadata-driven plugin rather than a worker wrapper. You register the plugin on the client
you already build, and it configures the client and worker options for you.

At connect time the plugin's client hook fetches the Cloud Run metadata once and caches it. The
metadata is three values gathered by `GoogleCloudRunMetadata.FetchAsync`:

* `Name` (the Temporal deployment name) from the `CLOUD_RUN_WORKER_POOL` environment variable, then
  the `K_SERVICE` environment variable.
* `Revision` from the `CLOUD_RUN_REVISION` environment variable, then the `K_REVISION` environment
  variable.
* `InstanceId` from the Cloud Run metadata server
  (`http://metadata.google.internal/computeMetadata/v1/instance/id`), read with the required
  `Metadata-Flavor: Google` request header. This is the only value not available as an environment
  variable, so the plugin makes a single HTTP GET at startup.

Cloud Run worker pools receive the `CLOUD_RUN_*` variables (and no `K_*` variables), while Cloud Run
services receive the `K_*` variables. The metadata server is available on both, so resolving the
name and revision in that order covers both deployment types. Worker pools are the primary target.

From those values:

* The client hook sets `TemporalConnectionOptions.Identity` to `WorkerIdentity`, which is
  `{InstanceId}@{Revision}`, falling back to `{InstanceId}@{Name}` when the revision is empty, or
  just `{InstanceId}` when both are empty. It only sets the identity when one is not already
  configured, so an explicitly configured identity wins.
* The worker hook sets `TemporalWorkerOptions.DeploymentOptions` to the `WorkerDeploymentVersion`
  whose deployment name is the Cloud Run name and whose build id is the Cloud Run revision, with
  `useWorkerVersioning: true` and a `VersioningBehavior.Pinned` default (a per-workflow behavior
  takes precedence). Each Cloud Run revision therefore maps to a worker deployment version.

If the metadata server cannot be reached, the plugin fails fast at connect time with a clear
`InvalidOperationException`, which usually means the process is not running on a Cloud Run worker
pool or service.

## Testing and advanced use

`WorkerIdPlugin` accepts a `WorkerIdPluginOptions` for tests and advanced scenarios. Set
`MetadataUri` / `Timeout` to point the fetch at a different endpoint, or set `Metadata` to a
pre-fetched `GoogleCloudRunMetadata` to skip the metadata server entirely:

```csharp
var metadata = await GoogleCloudRunMetadata.FetchAsync();
var plugin = new WorkerIdPlugin(new WorkerIdPluginOptions { Metadata = metadata });
```
