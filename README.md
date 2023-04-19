# Temporal .NET SDK

[![NuGet](https://img.shields.io/nuget/vpre/temporalio.svg?style=for-the-badge)](https://www.nuget.org/packages/Temporalio)
[![MIT](https://img.shields.io/github/license/temporalio/sdk-dotnet.svg?style=for-the-badge)](LICENSE)

[Temporal](https://temporal.io/) is a distributed, scalable, durable, and highly available orchestration engine used to
execute asynchronous, long-running business logic in a scalable and resilient way.

"Temporal .NET SDK" is the framework for authoring workflows and activities using .NET programming languages.

Also see:

* [NuGet Package](https://www.nuget.org/packages/Temporalio)
* [Application Development Guide](https://docs.temporal.io/application-development) (TODO: .NET docs)
* [API Documentation](https://dotnet.temporal.io/api)
* [Samples](https://github.com/temporalio/samples-dotnet)

⚠️ UNDER ACTIVE DEVELOPMENT

This SDK is under active development and has not released a stable version yet. APIs may change in incompatible ways
until the SDK is marked stable.

All features are present in this SDK, but some future features like static analyzers and source generation are not yet
present.

---

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
**Contents**

- [Quick Start](#quick-start)
- [Installation](#installation)
- [Implementing a Workflow and Activity](#implementing-a-workflow-and-activity)
- [Running a Worker](#running-a-worker)
- [Executing a Workflow](#executing-a-workflow)
- [Usage](#usage)
  - [Clients](#clients)
    - [Client via Dependency Injection](#client-via-dependency-injection)
    - [Data Conversion](#data-conversion)
  - [Workers](#workers)
    - [Worker as Generic Host](#worker-as-generic-host)
  - [Workflows](#workflows)
    - [Workflow Definition](#workflow-definition)
    - [Running Workflows](#running-workflows)
    - [Invoking Activities](#invoking-activities)
    - [Invoking Child Workflows](#invoking-child-workflows)
    - [Timers and Conditions](#timers-and-conditions)
    - [Workflow Task Scheduling and Cancellation](#workflow-task-scheduling-and-cancellation)
    - [Workflow Utilities](#workflow-utilities)
    - [Workflow Exceptions](#workflow-exceptions)
    - [Workflow Logic Constraints](#workflow-logic-constraints)
      - [.NET Task Determinism](#net-task-determinism)
      - [Workflow .editorconfig](#workflow-editorconfig)
    - [Workflow Testing](#workflow-testing)
      - [Automatic Time Skipping](#automatic-time-skipping)
      - [Manual Time Skipping](#manual-time-skipping)
      - [Mocking Activities](#mocking-activities)
    - [Workflow Replay](#workflow-replay)
  - [Activities](#activities)
    - [Activity Definition](#activity-definition)
    - [Activity Execution Context](#activity-execution-context)
    - [Activity Heartbeating and Cancellation](#activity-heartbeating-and-cancellation)
    - [Activity Worker Shutdown](#activity-worker-shutdown)
    - [Activity Testing](#activity-testing)
- [Development](#development)
  - [Build](#build)
  - [Code formatting](#code-formatting)
    - [VisualStudio Code](#visualstudio-code)
  - [Testing](#testing)
  - [Rebuilding Rust extension and interop layer](#rebuilding-rust-extension-and-interop-layer)
  - [Regenerating protos](#regenerating-protos)
  - [Regenerating API docs](#regenerating-api-docs)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## Quick Start

## Installation

Add the `Temporalio` package from [NuGet](https://www.nuget.org/packages/Temporalio). For example, using the `dotnet`
CLI:

    dotnet add package Temporalio --prerelease

**NOTE: This README is for the current branch and not necessarily what's released on NuGet.**

## Implementing a Workflow and Activity

Assuming the [ImplicitUsings](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#implicitusings) is
enabled, create an activity by putting the following in `MyActivities.cs`:

```csharp
namespace MyNamespace;

using Temporalio.Activities;

public class MyActivities
{
    // We need a "ref" so we can reference instance methods from workflows in a type-safe way
    public static readonly MyActivities Ref = ActivityRefs.Create<MyActivities>();

    // Activities can be async and/or static too! We just demonstrate instance
    // methods since many will use them that way.
    [Activity]
    public string SayHello(string name) => $"Hello, {name}!";
}
```

That creates the activity. Now to create the workflow, put the following in `SayHelloWorkflow.workflow.cs`:

```csharp
namespace MyNamespace;

using Temporalio.Workflows;

[Workflow]
public class SayHelloWorkflow
{
    // A "ref" is needed to access methods on this class in a type-safe way from
    // the client without instantiating the class
    public static readonly SayHelloWorkflow Ref = WorkflowRefs.Create<SayHelloWorkflow>();

    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        // This workflow just runs a simple activity to completion.
        // StartActivityAsync could be used to just start and there are many
        // other things that you can do inside a workflow.
        return await Workflow.ExecuteActivityAsync(
            // If the activity is static, we don't need Ref
            MyActivities.Ref.SayHello,
            name,
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }
}
```

This is a simple workflow that executes the `SayHello` activity.

## Running a Worker

To run this in a worker, put the following in `Program.cs`:

```csharp
using MyNamespace;
using Temporalio.Client;
using Temporalio.Worker;

// Create a client to localhost on "default" namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

// Cancellation token to shutdown worker on ctrl+c
using var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

// Create an activity instance since we have instance activities. If we had
// all static activities, we could just reference those directly.
var activities = new MyActivities();

// Create worker with the activity and workflow registered
using var worker = new TemporalWorker(
    client,
    new(taskQueue: "my-task-queue")
    {
        Activities = { activities.SayHello },
        Workflows = { typeof(SayHelloWorkflow) },
    });

// Run worker until cancelled
Console.WriteLine("Running worker");
try
{
    await worker.ExecuteAsync(tokenSource.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Worker cancelled");
}
```

When executed, this will listen for Temporal server requests to perform workflow and activity invocations.

## Executing a Workflow

To start and wait on a workflow result, with the worker program running elsewhere, put the following in a different
project's `Program.cs` that references the worker project:

```csharp
using MyNamespace;
using Temporalio.Client;

// Create a client to localhost on "default" namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

// Run workflow
var result = await client.ExecuteWorkflowAsync(
    SayHelloWorkflow.Ref.RunAsync,
    "Temporal",
    new(id: "my-workflow-id", taskQueue: "my-task-queue"));

Console.WriteLine("Workflow result: {0}", result);
```

This will output:

    Workflow result: Hello, Temporal!

## Usage

### Clients

A client can be created and used to start a workflow. For example:

```csharp
using MyNamespace;
using Temporalio.Client;

// Create client connected to server at the given address and namespace
var client = await TemporalClient.ConnectAsync(new()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
});

// Start a workflow
var handle = await client.StartWorkflowAsync(
    MyWorkflow.Ref.RunAsync,
    "some workflow argument",
    new() { ID = "my-workflow-id", TaskQueue = "my-task-queue" });

// Wait for a result
var result = await handle.GetResultAsync();
Console.WriteLine("Result: {0}", result);
```

Notes about the above code:

* Temporal clients are not explicitly disposable.
* To enable TLS, the `Tls` option can be set to a non-null `TlsOptions` instance.
* Instead of `StartWorkflowAsync` + `GetResultAsync` above, there is an `ExecuteWorkflowAsync` extension method that is
  clearer if the handle is not needed.
* Non-typesafe forms of `StartWorkflowAsync` and `ExecuteWorkflowAsync` exist when there is no workflow definition or
  the workflow may take more than one argument or some other dynamic need. These simply take string workflow type names
  and an object array for arguments.
* The `handle` above represents a `WorkflowHandle` which has specific workflow operations on it. For existing workflows,
  handles can be obtained via `client.GetWorkflowHandle`.

#### Client via Dependency Injection

Currently dependency injection for clients is done like any other async dependency. There are not yet any helpers to
make this easier. There is a [known issue](https://github.com/temporalio/sdk-dotnet/issues/44) where creating the client
outside of the dependency injection container causes it to not be usable via dependency injection so it must be created
within the container.

The current suggestion is just to make the `Task<TemporalClient>` a singleton. For example, in an ASP.NET application:

```csharp
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(ctx =>
    TemporalClient.ConnectAsync(new()
    {
        TargetHost = "localhost:7233",
        LoggerFactory = ctx.GetRequiredService<ILoggerFactory>(),
    }));

// Can then be used in handlers
var app = builder.Build();
app.MapGet("/", async (Task<TemporalClient> clientTask) =>
{
    var client = await clientTask;
    return await client.ExecuteWorkflowAsync(
        MyWorkflow.Ref.RunAsync,
        new(id: "my-workflow-id", taskQueue: "my-task-queue"));
});
app.Run();
```

Or from a generic host application:

```csharp
using Temporalio.Client;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(svcs =>
        svcs.AddSingleton(ctx => TemporalClient.ConnectAsync(new()
        {
            TargetHost = "localhost:7233",
            LoggerFactory = ctx.GetRequiredService<ILoggerFactory>(),
        })))
    .Build();
```

This can be wrapped in a client "provider" or other similar async task wrapper if needed.

#### Data Conversion

Data converters are used to convert raw Temporal payloads to/from actual .NET types. A custom data converter can be set
via the `DataConverter` option when creating a client. Data converters are a combination of payload converters, payload
codecs, and failure converters. Payload converters convert .NET values to/from serialized bytes. Payload codecs convert
bytes to bytes (e.g. for compression or encryption). Failure converters convert exceptions to/from serialized failures.

Data converters are in the `Temporalio.Converters` namespace. The default data converter uses a default payload
converter, which supports the following types:

* `null`
* `byte[]`
* `Google.Protobuf.IMessage` instances
* Anything that `System.Text.Json` supports

Custom converters can be created for all uses. Due to potential sandboxing use, payload converters must be specified as
types not instances. For example, to create client with a data converter that converts all C# property names to camel
case, you would:

```csharp
using System.Text.Json;
using Temporalio.Client;
using Temporalio.Converters;

public class CamelCasePayloadConverter : DefaultPayloadConverter
{
    public CamelCasePayloadConverter()
      : base(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
    {
    }
}

var client = await TemporalClient.ConnectAsync(new()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
    DataConverter = DataConverter.Default with { PayloadConverterType = typeof(CamelCasePayloadConverter) },
});
```

### Workers

Workers host workflows and/or activites. Here's how to run a worker::

```csharp
using MyNamespace;
using Temporalio.Client;
using Temporalio.Worker;

// Create client
var client = await TemporalClient.ConnectAsync(new ()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
});

// Create worker
using var worker = new TemporalWorker(client, new ()
{
    TaskQueue = "my-task-queue",
    Activities = { MyActivities.MyActivity },
    Workflows = { typeof(MyWorkflow) },
});

// Run worker until Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};
await worker.ExecuteAsync(cts.Token);
```

Notes about the above code:

* This shows how to run a worker from C# using top-level statements. Of course this can be part of a larger program and
  `ExecuteAsync` can be used like any other task call with a cancellation token.
* The worker uses the same client that is used for all other Temporal tasks (e.g. starting workflows).
* Workers can have many more options not shown here (e.g. data converters and interceptors).

#### Worker as Generic Host

It is not a coincidence that one of the overloads of `ExecuteAsync` has the same signature as
`Microsoft.Extensions.Hosting.BackgroundService.ExecuteAsync`. So to implement `BackgroundService`, you can do:

```csharp
using Temporalio.Client;
using Temporalio.Worker;

public sealed class MyWorker : BackgroundService
{
    private readonly ILoggerFactory loggerFactory;

    public Worker(ILoggerFactory loggerFactory) => this.loggerFactory = loggerFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var worker = new TemporalWorker(
            await TemporalClient.ConnectAsync(new()
            {
                TargetHost = "localhost:7233",
                LoggerFactory = loggerFactory,
            }),
            new()
            {
                TaskQueue = "my-task-queue",
                Activities = { MyActivities.MyActivity },
                Workflows = { typeof(MyWorkflow) },
            });
        await worker.ExecuteAsync(stoppingToken);
    }
}
```

Then you can configure it like:

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(svcs => svcs.AddHostedService<MyWorker>())
    .Build();
host.Run();
```

### Workflows

#### Workflow Definition

Workflows are defined as classes or interfaces with a `[Workflow]` attribute. The entry point method for a workflow has
the `[WorkflowRun]` attribute. Methods for signals and queries have the `[WorkflowSignal]` and `[WorkflowQuery]`
attributes respectively. Here is an example of a workflow definition:

```csharp
using Microsoft.Extensions.Logging;
using Temporalio.Workflow;

public record GreetingParams(string Salutation = "Hello", string Name = "<unknown>");

[Workflow]
public class GreetingWorkflow
{
    public static readonly GreetingWorkflow Ref = WorkflowRefs.Create<GreetingWorkflow>();

    private string? currentGreeting;
    private GreetingParams? greetingParamsUpdate;
    private bool complete;

    [WorkflowRun]
    public async Task<string> RunAsync(GreetingParams initialParams)
    {
        var greetingParams = initialParams;
        while (true)
        {
            // Call activity to create greeting and store as field
            currentGreeting = await Workflow.ExecuteActivityAsync(
                GreetingActivities.CreateGreeting,
                greetingParams,
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            Workflow.Logger.LogDebug("Greeting set to {Greeting}", currentGreeting);

            // Wait for param update or complete signal. Note, cancellation can
            // occur by default on WaitConditionAsync calls so cancellation
            // token does not need to be passed explicitly.
            var waitUpdate = Workflow.WaitConditionAsync(() => greetingParamsUpdate != null);
            var waitComplete = Workflow.WaitConditionAsync(() => complete);
            if (waitComplete == await Task.WhenAny(waitUpdate, waitComplete))
            {
                // Just return the greeting
                return currentGreeting!;
            }
            // We know it was an update, so update it and continue
            greetingParams = greetingParamsUpdate!;
            greetingParamsUpdate = null;
        }
    }

    [WorkflowSignal]
    public async Task UpdateGreetingParamsAsync(GreetingParams greetingParams) =>
      this.greetingParamsUpdate = greetingParams;

    [WorkflowSignal]
    public async Task CompleteWithGreetingAsync() => this.complete = true;

    [WorkflowQuery]
    public string CurrentGreeting() => currentGreeting;
}
```

Notes about the above code:

* The workflow client needs the ability to reference these instance methods, but C# doesn't allow referencing instance
  methods without an instance. Therefore we add a readonly `Ref` instance which is a proxy instance just for method
  references.
  * This is backed by either `GetUninitializedObject` for classes or a dynamic proxy generator for interfaces, but
    method invocations should never be made on it. It is only for referencing methods.
  * This is technically not needed. Any way that the method can be referenced for a client is acceptable.
  * Source generators will provide an additional, alternative way to use workflows in a typed way in the future.
* Interfaces and abstract methods can have these attributes. This is helpful for defining a workflow implemented
  elsewhere. But if/when implemented, all pieces of the implementation should have the attributes too.
* This workflow continually updates the greeting params when signalled and can complete with the greeting when given a
  different signal
* Workflow code must be deterministic. See the "Workflow Logic Constraints" section below.
* `Workflow.ExecuteActivityAsync` is strongly typed, so if the parameter was not a `GreetingParams` instance,
  compilation would fail.

Attributes that can be applied:

* `[Workflow]` attribute must be present on the workflow type.
  * The attribute can have a string argument for the workflow type name. Otherwise the name is defaulted to the
    unqualified type name (with the `I` prefix removed if on an interface and has a capital letter following).
* `[WorkflowRun]` attribute must be present on one and only one public method.
  * The workflow run method must return a `Task` or `Task<>`.
  * The workflow run method _should_ accept a single parameter and return a single type. Records are encouraged because
    optional fields can be added without affecting backwards compatibility of the workflow definition.
  * The parameters of this method and its return type are considered the parameters and return type of the workflow
    itself.
  * This attribute is not inherited and this method must be explicitly defined on the declared workflow type. Even if
    the method functionality should be inherited, for clarity reasons it must still be explicitly defined even if it
    just invokes the base class method.
* `[WorkflowSignal]` attribute may be present on any public method that handles signals.
  * Signal methods must return a `Task`.
  * The attribute can have a string argument for the signal name. Otherwise the name is defaulted to the unqualified
    method name with `Async` trimmed off the end if it is present.
  * This attribute is not inherited and therefore must be explicitly set on any override.
* `[WorkflowQuery]` attribute may be present on any public method that handles queries.
  * Query methods must be non-`void` but cannot return a `Task` (i.e. they cannot be async).
  * The attribute can have a string argument for the query name. Otherwise the name is defaulted to the unqualified
    method name.
  * This attribute is not inherited and therefore must be explicitly set on any override.

#### Running Workflows

To start a workflow from a client, you can `StartWorkflowAsync` and then use the resulting handle:

```csharp
// Start the workflow
var handle = await client.StartWorkflowAsync(
    GreetingWorkflow.Ref.RunAsync,
    new GreetingParams(Name: "Temporal"),
    new(id: "my-workflow-id", taskQueue: "my-task-queue"));
// Check current greeting via query
Console.WriteLine(
    "Current greeting: {0}",
    await handle.QueryWorkflowAsync(GreetingWorkflow.Ref.CurrentGreeting));
// Change the params via signal
await handle.SignalWorkflowAsync(
    GreetingWorkflow.Ref.UpdateGreetingParamsAsync,
    new GreetingParams(Salutation: "Aloha", Name: "John"));
// Tell it to complete via signal
await handle.SignalWorkflowAsync(GreetingWorkflow.Ref.CompleteWithGreetingAsync);
// Wait for workflow result
Console.WriteLine(
    "Final greeting: {0}",
    await handle.GetResultAsync());
```

Some things to note about the above code:

* This uses the `GreetingWorkflow` from the previous section.
* The output of this code is "Current greeting: Hello, Temporal!" and "Final greeting: Aloha, John!".
* ID and task queue are required for starting a workflow.
* All calls here are typed. For example, using something besides `GreetingParams` for the parameter of
  `StartWorkflowAsync` would be a compile-time failure.
* The `handle` is also typed with the workflow result, so `GetResultAsync()` returns a `string` as expected.
* A shortcut extension `ExecuteWorkflowAsync` is available that is just `StartWorkflowAsync` + `GetResultAsync`.

#### Invoking Activities

* Activities are executed with `Workflow.ExecuteActivityAsync` which accepts an activity method delegate + argument if
  required, or a string name + object array of arguments.
* Activity options are a simple class set after any activity and its argument(s).
  * These options must be present and either `ScheduleToCloseTimeout` or `StartToCloseTimeout` must be present.
  * Retry policy, cancellation type, etc can also be set on the options.
  * Cancellation token is defaulted as the workflow cancellation token, but an alternative can be given in options.
    When the token is cancelled, a cancellation request is sent to the activity. How that is handled depends on
    cancellation type.
* Activity failures are thrown from the task as `ActivityFailureException`.
* `ExecuteLocalActivityAsync` exists with mostly the same options for local activities.

#### Invoking Child Workflows

* Child workflows are started with `Workflow.StartChildWorkflowAsync` which accepts a workflow run method delegate +
  argument if required, or a string name + object array of arguments.
* Child workflow options are a simple class set after any child workflow run method and its argument(s).
  * These options are optional.
  * Retry policy, ID, etc can also be set on the options.
  * Cancellation token is defaulted as the workflow cancellation token, but an alternative can be given in options.
    When the token is cancelled, a cancellation request is sent to the child workflow. How that is handled depends on
    cancellation type.
* Result of a child workflow starting is a `ChildWorkflowHandle` which has the `ID`, `GetResultAsync` for getting the
  result, and `SignalAsync` for signalling the child.
* The task for starting a child workflow does not complete until the start has been accepted by the server.
* A shortcut of `Workflow.ExecuteChildWorkflowAsync` is available which is `StartChildWorkflowAsync` + `GetResultAsync`
  for those only needing to wait on its result.

#### Timers and Conditions

* A timer is represented by `Workflow.DelayAsync`.
  * Timers are also started on `Workflow.WaitConditionAsync` when a timeout is specified.
  * This can accept a cancellation token, but if none given, defaults to `Workflow.CancellationToken`.
  * `Task.Delay` or any other .NET timer-related call cannot be used in workflows because workflows must be
    deterministic. See the "Workflow Logic Constraints" section below.
* `Workflow.WaitConditionAsync` accepts a function that, when it returns true, the `Task` is completed successfully.
  * The function is invoked on each iteration of the internal event loop. This is commonly used for checking if a
    variable is changed from some other part of a workflow (e.g. a signal handler).
  * A timeout can be provided for the wait condition which uses a timer.
  * This can accept a cancellation token, but if none given, defaults to `Workflow.CancellationToken`.

#### Workflow Task Scheduling and Cancellation

Workflows are backed by a custom, deterministic
[TaskScheduler](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler?view=net-7.0). All
async calls inside a workflow must use this scheduler (i.e. `TaskScheduler.Current`) and not the default
thread-pool-based one (i.e. `TaskScheduler.Default`). See "Workflow Logic Constraints" on what to avoid to make sure the
proper task scheduler is used.

Every workflow contains a cancellation token at `Workflow.CancellationToken`. This token is cancelled when the workflow
is cancelled. For all workflow calls that accept a cancellation token, this is the default. So if a workflow is waiting
on `ExecuteActivityAsync` and the workflow is cancelled, that cancellation will propagate to the waiting activity.

Cancellation token sources may be used to perform cancellation more specifically. A cancellation token derived from the
workflow one can be created via `CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken)`. Then that
source can be used to cancel something more specifically. Or, in cases where cleanup code may need to be run during
cancellation such as in a `finally` block, a new unlinked cancellation token source can be constructed that will not be
seen as cancelled even though the workflow is cancelled.

Like in other areas of .NET, cancellation tokens must be respected in order to properly cancel the workflow. Yet for
most use cases where await calls yield to Temporal, the default cancellation token at the workflow level is good enough.

#### Workflow Utilities

In addition to the pieces documented above, additional properties/methods are statically available on `Workflow` that
can be used from workflows including:

* Properties:
  * `Info` - Immutable workflow info.
  * `InWorkflow` - Boolean saying whether the current code is running in a workflow. This is the only call that won't
    throw an exception when accessed outside of a workflow.
  * `Logger` - Scoped replay-aware logger for use inside a workflow. Normal loggers should not be used because they may
    log duplicate values during replay.
  * `Memo` - Read-only current memo values.
  * `Queries` - Mutable set of query definitions for the workflow. Technically this can be mutated to add query
    definitions at runtime, but methods with the `[WorkflowQuery]` attribute are strongly preferred.
  * `Random` - Deterministically seeded random instance for use inside workflows.
  * `TypedSearchAttributes` - Read-only current search attribute values.
  * `UtcNow` - Deterministic value for the current `DateTime`.
  * `Unsafe.IsReplaying` - For advanced users to know whether the workflow is replaying. This is rarely needed.
* Methods:
  * `CreateContinueAsNewException` - Create exception that can be thrown to perform a continue-as-new on the workflow.
    There are several overloads to properly type workflow arguments similar to start/execute workflow calls elsewhere.
  * `GetExternalWorkflowHandle` - Get a handle to an external workflow to issue cancellation requests and signals.
  * `NewGuid` - Create a deterministically random UUIDv4 GUID.
  * `Patched` and `DeprecatePatch` - Support for patch-based versioning inside the workflow.
  * `UpsertMemo` - Update the memo values for the workflow.
  * `UpsertTypedSearchAttributes` - Update the search attributes for the workflow.

#### Workflow Exceptions

* Workflows can throw exceptions to fail the workflow or the "workflow task" (i.e. suspend the workflow, retrying until
  code update allows it to continue).
* Exceptions that are instances of `Temporalio.Exceptions.FailureException` will fail the workflow with that exception.
  * For failing the workflow explicitly with a user exception, explicitly throw
    `Temporalio.Exceptions.ApplicationFailureException`. This can be marked non-retryable or include details as needed.
  * Other exceptions that come from activity execution, child execution, cancellation, etc are already instances of
    `FailureException` (or `TaskCanceledException`) and will fail the workflow if uncaught.
* All other exceptions fail the "workflow task" which means the workflow will continually retry until the workflow is
  fixed. This is helpful for bad code or other non-predictable exceptions. To actually fail the workflow, use an
  `ApplicationFailureException` as mentioned above.

#### Workflow Logic Constraints

Temporal Workflows [must be deterministic](https://docs.temporal.io/workflows#deterministic-constraints) which includes
.NET workflows. This means there are several things workflows cannot do such as:

* Perform IO (network, disk, stdio, etc)
* Access/alter external mutable state
* Do any threading
* Do anything using the system clock (e.g. `DateTime.Now`)
  * This includes  .NET timers (e.g. `Task.Delay` or `Thread.Sleep`)
* Make any random calls
* Make any not-guaranteed-deterministic calls (e.g. iterating over a dictionary)

In the future, an analyzer may be written to help catch some of these mistakes at compile time. In the meantime, due to
.NET's lack of a sandbox, there is not a good way to prevent non-deterministic calls so developers need to be vigilant.

##### .NET Task Determinism

Some calls in .NET do unsuspecting non-deterministic things and are easy to accidentally use. This is especially true
with `Task`s. Temporal requires that the deterministic `TaskScheduler.Current` is used, but many .NET async calls will
use `TaskScheduler.Default` implicitly (and some analyzers even encourage this). Here are some known gotchas to avoid
with .NET tasks inside of workflows:

* Do not use `Task.Run` - this uses the default scheduler and puts work on the thread pool.
  * Use `Task.Factory.StartNew` or instantiate the `Task` and run `Task.Start` on it.
* Do not use `Task.ConfigureAwait(false)` - this will not use the current context.
  * If you must use `Task.ConfigureAwait`, use `Task.ConfigureAwait(true)`.
  * There is no significant performance benefit to `Task.ConfigureAwait` in workflows anyways due to how the scheduler
    works.
* Do not use anything that defaults to the default task scheduler.
* Do not use `Task.Delay`, `Task.Wait`, timeout-based `CancellationTokenSource`, or anything that uses .NET built-in
  timers.
  * `Workflow.DelayAsync`, `Workflow.WaitConditionAsync`, or non-timeout-based cancellation token source is suggested.
* Be wary of additional libraries' implicit use of the default scheduler.
  * For example, while there are articles for `Dataflow` about
    [using a specific scheduler](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-specify-a-task-scheduler-in-a-dataflow-block), there are hidden implicit uses of `TaskScheduler.Default`. For
    example, see [this bug](https://github.com/dotnet/runtime/issues/83159).

In order to help catch wrong scheduler use, by default the Temporal .NET SDK adds an event source listener for
info-level task events. While this technically receives events from all uses of tasks in the process, we make sure to
ignore anything that is not running in a workflow in a high performant way (basically one thread local check). For code
that does run in a workflow and accidentally starts a task in another scheduler, an `InvalidWorkflowOperationException`
will be thrown which "pauses" the workflow (fails the workflow task which continually retries until the code is fixed.).
This is unfortunately a runtime-only check, but can help catch mistakes early. If this needs to be turned off for any
reason, set `DisableWorkflowTracingEventListener` to `true` in worker options.

In the near future for modern .NET versions we hope to use the
[new `TimeProvider` API](https://github.com/dotnet/runtime/issues/36617) which will allow us to control current time and
timers.

##### Workflow .editorconfig

Since workflow code follows some different logic rules than regular C# code, there are some common analyzer rules out
there that developers may want to disable. To ensure these are only disabled for workflows, current recommendation is to
use the `.workflow.cs` extension for files containing workflows.

Here are the rules to disable:

* [CA1024](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1024) - This encourages
  properties instead of methods that look like getters. However for reflection reasons we cannot use property getters
  for queries, so it is very normal to have

    ```csharp
    [WorkflowQuery]
    public string GetSomeThing() => someThing;
    ```

* [CA1822](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1822) - This encourages
  static methods when methods don't access instance state. Workflows however often use instance methods for run,
  signals, or queries even if they could be static.
* [CA2007](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007) - This encourages
  users to use `ConfigureAwait` instead of directly waiting on a task. But in workflows, there is no benefit to this and
  it just adds noise (and if used, needs to be `ConfigureAwait(true)` not `ConfigureAwait(false)`).
* [CA2008](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2008) - This encourages
  users to always apply an explicit task scheduler because the default of `TaskScheduler.Current` is bad. But for
  workflows, the default of `TaskScheduler.Current` is good/required.
* [CA5394](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca5394) - This discourages
  use of non-crypto random. But deterministic workflows, via `Workflow.Random` intentionally provide a deterministic
  non-crypto random instance.
* `CS1998` - This discourages use of `async` on async methods that don't `await`. But workflows handlers like signals
  are often easier to write in one-line form this way, e.g.
  `public async Task SignalSomething(string value) => this.value = value;`.
* [VSTHRD105](https://github.com/microsoft/vs-threading/blob/main/doc/analyzers/VSTHRD105.md) -  This is similar to
  `CA2008` above in that use of implicit current scheduler is discouraged. That does not apply to workflows where it is
  encouraged/required.

Here is the `.editorconfig` snippet for the above which may frequently change as we learn more:

```ini
##### Configuration specific for Temporal workflows #####
[*.workflow.cs]

# We use getters for queries, they cannot be properties
dotnet_diagnostic.CA1024.severity = none

# Don't force workflows to have static methods
dotnet_diagnostic.CA1822.severity = none

# Do not need ConfigureAwait for workflows
dotnet_diagnostic.CA2007.severity = none

# Do not need task scheduler for workflows
dotnet_diagnostic.CA2008.severity = none

# Workflow randomness is intentionally deterministic
dotnet_diagnostic.CA5394.severity = none

# Allow async methods to not have await in them
dotnet_diagnostic.CS1998.severity = none

# Don't avoid, but rather encourage things using TaskScheduler.Current in workflows
dotnet_diagnostic.VSTHRD105.severity = none
```

#### Workflow Testing

Workflow testing can be done in an integration-test fashion against a real server, however it is hard to simulate
timeouts and other long time-based code. Using the time-skipping workflow test environment can help there.

A non-time-skipping `Temporalio.Testing.WorkflowEnvironment` can be started via `StartLocalAsync` which supports all
standard Temporal features. It is actually a real Temporal server lazily downloaded on first use and run as a
sub-process in the background.

A time-skipping `Temporalio.Testing.WorkflowEnvironment` can be started via `StartTimeSkippingAsync` which is a
reimplementation of the Temporal server with special time skipping capabilities. This too lazily downloads the process
to run when first called. Note, this class is not thread safe nor safe for use with independent tests. It can be reused,
but only for one test at a time because time skipping is locked/unlocked at the environment level.

##### Automatic Time Skipping

Anytime a workflow result is waiting on, the time-skipping server automatically advances to the next event it can. To
manually advance time before waiting on the result of the workflow, the `WorkflowEnvironment.DelayAsync` method can be
used. If an activity is running, time-skipping is disabled.

Here's a simple example of a workflow that sleeps for 24 hours:

```csharp
using Temporalio.Workflows;

[Workflow]
public class WaitADayWorkflow
{
    public static readonly WaitADayWorkflow Ref = WorkflowRefs.Create<WaitADayWorkflow>();

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        await Workflow.DelayAsync(TimeSpan.FromDays(1));
        return "all done";
    }
}
```

A regular integration test of this workflow on a normal server would be way too slow. However, the time-skipping server
automatically skips to the next event when we wait on the result. Here's a test for that workflow:

```csharp
using Temporalio.Testing;
using Temporalio.Worker;

[Fact]
public async Task WaitADayWorkflow_SimpleRun_Succeeds()
{
    await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
    using var worker = new TemporalWorker(env.Client, new()
    {
        TaskQueue = $"tq-{Guid.NewGuid()}",
        Workflows = { typeof(WaitADayWorkflow) },
    });
    var result = await env.Client.ExecuteWorkflowAsync(
        WaitADayWorkflow.Ref.RunAsync,
        new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
    Assert.Equal("all done", result);
}
```

This test will run almost instantly. This is because by calling `ExecuteWorkflowAsync` on our client, we are actually
calling `StartWorkflowAsync` + `GetResultAsync`, and `GetResultAsync` automatically skips time as much as it can
(basically until the end of the workflow or until an activity is run).

To disable automatic time-skipping while waiting for a workflow result, run code as a lambda passed to
`env.WithAutoTimeSkippingDisabled` or `env.WithAutoTimeSkippingDisabledAsync`.

##### Manual Time Skipping

Until a workflow is waited on, all time skipping in the time-skipping environment is done manually via
`WorkflowEnvironment.DelayAsync`.

Here's a workflow that waits for a signal or times out:

```csharp
using Temporalio.Workflows;

[Workflow]
public class SignalWorkflow
{
    public static readonly SignalWorkflow Ref = WorkflowRefs.Create<SignalWorkflow>();

    private bool signalReceived = false;

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // Wait for signal or timeout in 45 seconds
        if (Workflow.WaitConditionAsync(() => signalReceived, TimeSpan.FromSeconds(45)))
        {
            return "got signal";
        }
        return "got timeout";
    }

    [WorkflowSignal]
    public async Task SomeSignalAsync() => signalReceived = true;
}
```

To test a normal signal, you might:

```csharp
using Temporalio.Testing;
using Temporalio.Worker;

[Fact]
public async Task SignalWorkflow_SendSignal_HasExpectedResult()
{
    await using var env = WorkflowEnvironment.StartTimeSkippingAsync();
    using var worker = new TemporalWorker(env.Client, new()
    {
        TaskQueue = $"tq-{Guid.NewGuid()}",
        Workflows = { typeof(SignalWorkflow) },
    });
    var handle = await env.Client.StartWorkflowAsync(
        SignalWorkflow.Ref.RunAsync,
        new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
    await handle.SignalAsync(SignalWorkflow.Ref.SomeSignalAsync);
    Assert.Equal("got signal", await handle.GetResultAsync());
}
```

But how would you test the timeout part? Like so:

```csharp
using Temporalio.Testing;
using Temporalio.Worker;

[Fact]
public async Task SignalWorkflow_SignalTimeout_HasExpectedResult()
{
    await using var env = WorkflowEnvironment.StartTimeSkippingAsync();
    using var worker = new TemporalWorker(env.Client, new()
    {
        TaskQueue = $"tq-{Guid.NewGuid()}",
        Workflows = { typeof(SignalWorkflow) },
    });
    var handle = await env.Client.StartWorkflowAsync(
        SignalWorkflow.Ref.RunAsync,
        new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
    await env.DelayAsync(TimeSpan.FromSeconds(50));
    Assert.Equal("got timeout", await handle.GetResultAsync());
}
```

##### Mocking Activities

When testing workflows, often you don't want to actually run the activities. Activities are just functions with the
`[Activity]` attribute. Simply write different/empty/fake/asserting ones and pass those to the worker to have different
activities called during the test.

#### Workflow Replay

Given a workflow's history, it can be replayed locally to check for things like non-determinism errors. For example,
assuming the `history` parameter below is given a JSON string of history exported from the CLI or web UI, the following
function will replay it:

```csharp
using Temporalio;
using Temporalio.Worker;

public static async Task ReplayFromJsonAsync(string historyJson)
{
    var replayer = new WorkflowReplayer(new() { Workflows = { typeof(MyWorkflow) } });
    await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson("my-workflow-id", historyJson));
}
```

If there is a non-determinism, this will throw an exception.

Workflow history can be loaded from more than just JSON. It can be fetched individually from a workflow handle, or even
in a list. For example, the following code will check that all workflow histories for a certain workflow type (i.e.
workflow class) are safe with the current workflow code.

```csharp
using Temporalio;
using Temporalio.Client;
using Temporalio.Worker;

public static async Task CheckPastHistoriesAysnc(ITemporalClient client)
{
    var replayer = new WorkflowReplayer(new() { Workflows = { typeof(MyWorkflow) } });
    var listIter = client.ListWorkflowHistoriesAsync("WorkflowType = 'SayHello'");
    await foreach (var result in replayer.ReplayWorkflowsAsync(listIter))
    {
        if (result.ReplayFailure != null)
        {
            ExceptionDispatchInfo.Throw(result.ReplayFailure);
        }
    }
}
```

### Activities

#### Activity Definition

Activities are methods with the `[Activity]` annotation like so:

```csharp
namespace MyNamespace;

using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Temporalio.Activities;

public static class MyActivities
{
    private static readonly HttpClient client = new();

    [Activity]
    public static async Task<string> GetPageAsync(string url)
    {
        // Heartbeat every 2s
        using var timer = new Timer(2000)
        {
            AutoReset = true,
            Enabled = true,
        };
        timer.Elapsed += (sender, eventArgs) => ActivityExecutionContext.Current.Heartbeat();

        // Issue our HTTP call
        using var response = await client.GetAsync(url, ActivityExecutionContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ActivityExecutionContext.Current.CancellationToken);
    }
}
```

Notes about activity definitions:

* All activities must have the `[Activity]` attribute.
* `[Activity]` can be given a custom string name.
  * If unset, the default is the method's unqualified name. If the method name ends with `Async` and returns a `Task`,
    the default name will have `Async` trimmed off the end.
* Long running activities should heartbeat to regularly to inform server the activity is still running.
  * Heartbeats are throttled internally, so users can call this frequently without fear of calling too much.
  * Activities must heartbeat to receive cancellation.
* Activities can be defined on static or instance methods. They can even be lambdas or local methods, but rarely is this
  valuable since often an activity will be referenced by a workflow.
* Activities can be synchronous or asynchronous. If an activity returns a `Task`, that task is awaited on as part of the
  activity.

#### Activity Execution Context

During activity execution, an async-local activity context is available via `ActivityExecutionContext.Current`. This
will throw if not currently in an activity context (which can be checked with `ActivityExecutionContext.HasCurrent`). It
contains the following important members:

* `Info` - Information about the activity.
* `Logger` - A logger scoped to the activity.
* `CancelReason` - If `CancellationToken` is cancelled, this will contain the reason.
* `CancellationToken` - Token cancelled when the activity is cancelled.
* `Heartbeat(object?...)` - Send a heartbeat from this activity.
* `WorkerShutdownToken` - Token cancelled on worker shutdown before the grace period + `CancellationToken` cancellation.

#### Activity Heartbeating and Cancellation

In order for a non-local activity to be notified of cancellation requests, it must invoke
`ActivityExecutionContext.Current.Heartbeat()`. It is strongly recommended that all but the fastest executing activities
call this function regularly.

In addition to obtaining cancellation information, heartbeats also support detail data that is persisted on the server
for retrieval during activity retry. If an activity calls `ActivityExecutionContext.Current.Heartbeat(123)` and then
fails and is retried, `ActivityExecutionContext.Current.Info.HeartbeatDetails` will contain the last detail payloads. A
helper can be used to convert, so `await ActivityExecutionContext.Current.Info.HeartbeatDetailAtAsync<int>(0)` would
give `123` on the next attempt.

Heartbeating has no effect on local activities.

#### Activity Worker Shutdown

An activity can react to a worker shutdown specifically.

Upon worker shutdown, `ActivityExecutionContext.WorkerShutdownToken` is cancelled. Then the worker will wait a grace
period set by the `GracefulShutdownTimeout` worker option (default as 0) before issuing actual cancellation to all
still-running activities via `ActivityExecutionContext.CancellationToken`.

Worker shutdown will wait on all activities to complete, so if a long-running activity does not respect cancellation,
the shutdown may never complete.

#### Activity Testing

Unit testing an activity or any code that could run in an activity is done via the
`Temporalio.Testing.ActivityEnvironment` class. Simply instantiate the class, and any function passed to `RunAsync` will
be invoked inside the activity context. The following important members are available on the environment to affect the
activity context:

* `Info` - Activity info, defaulted to a basic set of values.
* `Logger` - Activity logger, defaulted to a null logger.
* `Cancel(CancelReason)` - Helper to set the reason and cancel the source.
* `CancelReason` - Cancel reason.
* `CancellationTokenSource` - Token source for issuing cancellation.
* `Heartbeater` - Callback invoked each heartbeat.
* `WorkerShutdownTokenSource` - Token source for issuing worker shutdown.

## Development

### Build

Prerequisites:

* [.NET](https://learn.microsoft.com/en-us/dotnet/core/install/)
* [Rust](https://www.rust-lang.org/) (i.e. `cargo` on the `PATH`)
* [Protobuf Compiler](https://protobuf.dev/) (i.e. `protoc` on the `PATH`)
* This repository, cloned recursively

With all prerequisites in place, run:

    dotnet build

Or for release:

    dotnet build --configuration Release

### Code formatting

This project uses StyleCop analyzers with some overrides in `.editorconfig`. To format, run:

    dotnet format

Can also run with `--verify-no-changes` to ensure it is formatted.

#### VisualStudio Code

When developing in vscode, the following JSON settings will enable StyleCop analyzers:

```json
    "omnisharp.enableEditorConfigSupport": true,
    "omnisharp.enableRoslynAnalyzers": true
```

### Testing

Run:

    dotnet test

Can add options like:

* `--logger "console;verbosity=detailed"` to show logs
* `--filter "FullyQualifiedName=Temporalio.Tests.Client.TemporalClientTests.ConnectAsync_Connection_Succeeds"` to run a
  specific test
* `--blame-crash` to do a host process dump on crash

To help debug native pieces and show full stdout/stderr, this is also available as an in-proc test program. Run:

    dotnet run --project tests/Temporalio.Tests

Extra args can be added after `--`, e.g. `-- -verbose` would show verbose logs and `-- --help` would show other
options. If the arguments are anything but `--help`, the current assembly is prepended to the args before sending to the
xUnit runner.

### Rebuilding Rust extension and interop layer

To regen core interop from header, install
[ClangSharpPInvokeGenerator](https://github.com/dotnet/ClangSharp#generating-bindings) like:

    dotnet tool install --global ClangSharpPInvokeGenerator

Then, run:

    ClangSharpPInvokeGenerator @src/Temporalio/Bridge/GenerateInterop.rsp

The Rust DLL is built automatically when the project is built. `protoc` may need to be on the `PATH` to build the Rust
DLL.

### Regenerating protos

Must have `protoc` on the `PATH`. Note, for now you must use `protoc` 3.x until
[our GH action downloader](https://github.com/arduino/setup-protoc/issues/33) is fixed or we change how we download
protoc and check protos (since protobuf
[changed some C# source](https://github.com/protocolbuffers/protobuf/pull/9981)).

Then:

    dotnet run --project src/Temporalio.Api.Generator

### Regenerating API docs

Install [docfx](https://dotnet.github.io/docfx/), then run:

    docfx src/Temporalio.ApiDoc/docfx.json
