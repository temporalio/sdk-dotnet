# OpenTelemetry Support

This extension adds OpenTelemetry tracing support to the [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet).

Temporal .NET SDK OpenTelemetry support consists of an interceptor that creates spans (i.e. .NET diagnostic activities)
to trace execution. A special approach must be taken for workflows since they can be interrupted and resumed elsewhere
while OpenTelemetry spans cannot.

Although generic .NET `System.Diagnostic` activities are used, this extension is OpenTelemetry specific due to the
propagation capabilities in use for serializing spans across processes/SDKs. For users needing generic tracing or other
similar features, this code can be used as a guide for how to write an interceptor.

⚠️ UNDER ACTIVE DEVELOPMENT

This SDK is under active development and has not released a stable version yet. APIs may change in incompatible ways
until the SDK is marked stable.

## Quick Start

Add the `Temporalio.Extensions.OpenTelemetry` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.OpenTelemetry). For example, using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.OpenTelemetry --prerelease

In addition to configuring the OpenTelemetry tracer provider with the proper sources, the
`Temporalio.Extensions.OpenTelemetry.TracingInterceptor` class must be set as the interceptor when creating a client.
For example, this sets up tracing to the console:

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Temporalio.Client;
using Temporalio.Extensions.OpenTelemetry;
using Temporalio.Worker;

// Setup the tracer provider
using var tracerProvider = Sdk.CreateTracerProviderBuilder().
    AddSource(
        TracingInterceptor.ClientSource.Name,
        TracingInterceptor.WorkflowsSource.Name,
        TracingInterceptor.ActivitiesSource.Name).
    AddConsoleExporter().
    Build();

// Create a client to localhost on "default" namespace with the tracing
// interceptor
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    Interceptors = new[] { new TracingInterceptor() },
});

// Now client and worker calls are traced...
```

## How it Works

The tracing interceptor uses OpenTelemetry context propagation to serialize the currently active diagnostic activity as
a Temporal header that is then built back into the activity context for use by downstream code.

Outbound calls like starting a workflow from a client or starting an activity from a workflow will create a diagnostic
activity and serialize it to the outbound Temporal header. Inbound calls like executing an activity or handling a signal
will deserialize the header as a context and use it for new diagnostic activities.

This means, notwithstanding workflow tracing caveats discussed later, if a client starts a workflow that executes an
activity that itself uses a Temporal client to start a workflow, the spans will be parented properly across all
processes. This works even across Temporal SDK languages.

## Client and Activity Tracing

When a client starts a workflow, sends a signal, or issues a query, a new diagnostic activity is created for the life of
the call. The diagnostic activity context is serialized to the Temporal header which is then received by the worker on
the workflow side and set as the current parent context for other diagnostic activities.

When a worker receives a Temporal activity execution, it starts a diagnostic activity for the entirety of the attempt,
recording an exception on failure.

Since these use .NET diagnostic activities in traditional ways, they can be combined with normal diagnostic activity
use. Therefore is it very normal to create a diagnostic activity that surrounds the client call or create a diagnostic
activity inside of the Temporal activity.

## Workflow Tracing

Both the .NET diagnostic API and OpenTelemetry require activities/spans to be started and stopped in process. This works
well for normal imperative code that is not distributed. But Temporal workflows are distributed functions that can be
interrupted and resumed elsewhere. .NET diagnostic activities cannot be resumed elsewhere so there is a feature gap that
must be taken into account.

OpenTelemetry supports propagating spans (i.e. .NET diagnostic activities) across process boundaries which Temporal uses
in all SDKs to resume traces across workers (and even across languages). However .NET
[does not support](https://github.com/dotnet/runtime/issues/86966) implicitly attaching the extracted span context, so
this feature gap must be taken into account.

Also, the way workflow code resumption is performed in Temporal is to deterministically replay already-executed steps.
Therefore, if those steps have external effects, Temporal must skip those during replay. Temporal does this with
`Workflow.Logger` by not logging during replay. However with .NET diagnostic activities, one cannot rehydrate an
existing diagnostic activity to not be recorded part of the time but still use it for proper parenting of new diagnostic
activities. Activities can only be created to start/stop, they cannot be created from existing span/trace IDs
deterministically. So a solution must be provided for this as well.

To recap, here are the traditional problems with using simple .NET/OpenTelemetry distributed tracing for workflows:

* .NET does not support resuming/recreating diagnostic activities or otherwise preemptible code
* .NET does not support an implicit parent diagnostic activity context, only implicit parent diagnostic activities
* .NET does not support deterministic span/trace IDs

### Solution

In order to have an implicit parent context without creating a diagnostic activity, and have it be somewhat resumable,
a wrapper for diagnostic activity context + diagnostic activity called `WorkflowActivity` has been created. This can be
just a context or the actual diagnostic activity. To create a diagnostic workflow activity, the `ActivitySource`
extension `StartWorkflowActivity` can be used. This will use the underlying diagnostic activity source, but will
immediately starts and stops the .NET diagnostic activity it wraps. This is because a diagnostic activity cannot
represent the time taken for the code within it due to the fact that it may complete in another process which .NET does
not allow. So the diagnostic activities time range in workflows is immediate and does not matter. However, parentage is
still mostly supported.

### How Workflow Tracing Works

On workflow inbound calls for executing a workflow, handling a signal, or handling a query, a new diagnostic activity is
created _only when not replaying_ (query is never considered "replaying" in this case) but a new diagnostic workflow
activity wrapper is always created with the context from the Temporal header (i.e. the diagnostic activity created on
client workflow start). Although the diagnostic activity is started-then-stopped immediately, it becomes the parent for
diagnostic activities in that same-worker-process cached workflow instance. Workflows are removed from cache (and
therefore replayed from beginning on next run) when they throw an exception or are forced out of the cache for LRU
reasons.

Diagnostic activities created for signals and queries are parented to the client-outbound diagnostic activity that
started the workflow, and only _linked_ to the client-outbound diagnostic activity that invoked the signal/query.

If a workflow fails or if a workflow task fails (i.e. workflow suspension due to workflow/signal exception that was not
a Temporal exception), a _new_ diagnostic activity is created representing that failure. The diagnostic activity
representing the run of the workflow is not only already completed (as they all are) but it may not even be created
because this could be replaying on a different worker.

Outbound calls from a workflow for scheduling an activity, scheduling a local activity, starting a child, signalling a
child, or signalling an external workflow will create a diagnostic activity _only when not replaying_. This is then
serialized into the header of the outbound call to be deserialized on the worker that accepts it.

Overall, this means that with no cache eviction (i.e. runs to completion on the same process it started without
non-Temporal exception), diagnostic activities will be properly ordered/parented. However, when a replay is needed, the
diagnostic activities may not be parented the same. But they will all be parented to the same outer diagnostic activity
that was created on client outbound. Also, in cases of task failure (or worker crash or similar), a diagnostic activity
may be duplicated since it's not "replaying" when Temporal is continually trying to proceed with new code past task
failure.

**⚠️WARNING** Do not use .NET diagnostic activity API inside of workflows. They are inherently non-deterministic and
can lead to unpredictable behavior/traces during replay (which often only surfaces in failure scenarios).

### Creating Diagnostic Activities in Workflows

Users can create their own diagnostic _workflow_ activities via the `ActivitySource` extension
`TrackWorkflowDiagnosticActivity`. This is `IDisposable` like a normal `System.Diagnostic.Activity`, but unlike that the
diagnostic activity, the diagnostic workflow activity may not result in a real diagnostic activity during replay. Also,
it is started/stopped immediately. It is simply placed as async-local until disposed so it can implicitly become the
parent of any others.

### Workflow Tracing Example

For example, take the following code:

```csharp
using Temporalio.Activities;
using Temporalio.Extensions.OpenTelemetry;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    public static readonly ActivitySource CustomSource = new("MyCustomSource");

    [WorkflowRun]
    public async Task RunAsync()
    {
        await Workflow.ExecuteActivityAsync(
            (MyActivities act) => act.DoThing1Async(),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        using (CustomSource.TrackWorkflowDiagnosticActivity("MyCustomActivity"))
        {
            await Workflow.ExecuteActivityAsync(
                (MyActivities act) => act.DoThing2Async(),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        }
    }
}
```

So running this workflow might have the following diagnostic activities in this hierarchy:

* `StartWorkflow:MyWorkflow`
  * `RunWorkflow:MyWorkflow`
    * `StartActivity:DoThing1`
      * `RunActivity:DoThing1`
    * `MyCustomActivity`
      * `StartActivity:DoThing2`
        * `RunActivity:DoThing2`
    * `CompleteWorkflow: MyWorkflow`

But if, say, the worker crashed after starting the first activity, it might look
like:

* `StartWorkflow:MyWorkflow`
  * `RunWorkflow:MyWorkflow`
    * `StartActivity:DoThing1`
      * `RunActivity:DoThing1`
  * `MyCustomActivity`
    * `StartActivity:DoThing2`
      * `RunActivity:DoThing2`
  * `CompleteWorkflow: MyWorkflow`

Notice how some diagnostic activities are now not under the `RunWorkflow:MyWorkflow`. This is because the workflow
resumes on a different process but, due to .NET and OpenTelemetry limitations, a diagnostic activity cannot be resumed
on a different process.