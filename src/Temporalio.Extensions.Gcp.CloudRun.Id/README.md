# Google Cloud Run worker identity support

This extension provides `CloudRunIdPlugin`, a Temporal client/worker plugin that derives a client
identity from Google Cloud Run instance metadata, for Cloud Run worker pools and services.

Add the `Temporalio.Extensions.Gcp.CloudRun.Id` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Gcp.CloudRun.Id). For example,
using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.Gcp.CloudRun.Id

## Quick Start

Construct a `CloudRunIdPlugin`, register it on your client connect options via `Plugins`, then run a
normal long-lived worker. Registering it once on the client is enough: the plugin sets the client
identity at connect time, and workers created from that client inherit it.

```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun.Id;
using Temporalio.Worker;

var connectOptions = new TemporalClientConnectOptions("my-namespace.a1b2c.tmprl.cloud:7233")
{
    Namespace = "my-namespace",
    // Register the plugin once on the client. It reads the Cloud Run metadata at connect time and
    // propagates to workers created from the connected client.
    Plugins = new[] { new CloudRunIdPlugin() },
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
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    shutdown.Cancel();
});

try
{
    await worker.ExecuteAsync(shutdown.Token);
}
catch (OperationCanceledException)
{
    // Expected shutdown path.
}
```

> **Note:** the plugin applies the identity when the client connects, so it only runs if your client
> connects with the plugin registered. If the client is created lazily — as some dependency-injection
> setups do — the plugin may not be applied; in that case set the identity yourself before building the client:

```csharp
var metadata = await GoogleCloudRunMetadata.FetchAsync();
connectOptions.Identity = metadata.Identity;
```

## How it works

You register the plugin on the client you already build, and it sets the client identity for you.

At connect time the plugin's client hook fetches the Cloud Run metadata once and caches it. The
metadata is three values gathered by `GoogleCloudRunMetadata.FetchAsync`:

* `Name` — the Cloud Run worker pool name (from the `CLOUD_RUN_WORKER_POOL` environment variable),
  or the service name (from `K_SERVICE`).
* `Revision` from the `CLOUD_RUN_REVISION` environment variable, then the `K_REVISION` environment
  variable.
* `InstanceId` from the Cloud Run metadata server
  (`http://metadata.google.internal/computeMetadata/v1/instance/id`), read with the required
  `Metadata-Flavor: Google` request header. This is the only value not available as an environment
  variable, so the plugin makes a single HTTP GET at startup.

Cloud Run worker pools receive the `CLOUD_RUN_*` variables (and no `K_*` variables), while Cloud Run
services receive the `K_*` variables. The metadata server is available on both, so resolving the
name and revision in that order covers both worker pools and services.

From those values the client hook sets `TemporalConnectionOptions.Identity` to `Identity`,
which is `{InstanceId}@{Revision}`, falling back to `{InstanceId}@{Name}` when the revision is empty,
or just `{InstanceId}` when both are empty. It only sets the identity when one is not already
configured, so an explicitly configured identity wins. Workers created from the connected client
inherit that identity.

If the metadata server cannot be reached, the plugin throws an `InvalidOperationException` at connect
time, which usually means the process is not running on a Cloud Run worker pool or service.
