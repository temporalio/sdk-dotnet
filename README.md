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

Notably missing from this SDK:

* Workflow workers

---

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
**Contents**

- [Quick Start](#quick-start)
- [Installation](#installation)
- [Implementing an Activity](#implementing-an-activity)
- [Running a Workflow](#running-a-workflow)
- [Usage](#usage)
  - [Client](#client)
  - [Data Conversion](#data-conversion)
  - [Workers](#workers)
  - [Workflows](#workflows)
    - [Workflow Definition](#workflow-definition)
    - [Workflow Task Scheduling](#workflow-task-scheduling)
  - [Activities](#activities)
    - [Activity Definition](#activity-definition)
    - [Activity Context](#activity-context)
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

## Implementing an Activity

Workflow implementation is not yet supported in the .NET SDK, but defining a workflow and implementing activities is.

For example, if you have a `SayHelloWorkflow` workflow in another Temporal language that invokes `SayHello` activity on
`my-activity-queue` in C#, you can have the following in a worker project's `Program.cs`:

```csharp
using Temporalio;
using Temporalio.Activity;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflow;

// Create client
var client = await TemporalClient.ConnectAsync(new ()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
});

// Create worker
using var worker = new TemporalWorker(client, new ()
{
    TaskQueue = "my-activity-queue",
    Activities = { SayHello },
});

// Run worker until Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};
await worker.ExecuteAsync(cts.Token);

// Implementation of an activity in .NET
[Activity]
string SayHello(string name) => $"Hello, {name}!";

// Definition of a workflow implemented in another language
namespace MyNamespace
{
    [Workflow]
    public interface ISayHelloWorkflow
    {
        static readonly ISayHelloWorkflow Ref = Refs.Create<ISayHelloWorkflow>();

        [WorkflowRun]
        Task<string> RunAsync(string name);
    }
}
```

Running that will start a worker.

## Running a Workflow

Then you can run the workflow, referencing that project in another project's `Program.cs`:

```csharp
using Temporalio.Client;

// Create client
var client = await TemporalClient.ConnectAsync(new()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
});

// Run workflow
var result = await client.ExecuteWorkflowAsync(
    MyNamespace.ISayHelloWorkflow.Ref.RunAsync,
    "Temporal",
    new() { ID = "my-workflow-id", TaskQueue = "my-workflow-queue" });

Console.WriteLine($"Workflow result: {result}");
```

This will output:

```
Workflow result: Hello, Temporal!
```

## Usage

### Client

A client can be created an used to start a workflow. For example:

```csharp
using Temporalio.Client;

// Create client connected to server at the given address and namespace
var client = await TemporalClient.ConnectAsync(new()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
});

// Start a workflow
var handle = await client.StartWorkflowAsync(
    IMyWorkflow.Ref.RunAsync,
    "some workflow argument",
    new() { ID = "my-workflow-id", TaskQueue = "my-workflow-queue" });

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

### Data Conversion

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

Workers host workflows and/or activities. Workflows cannot yet be written in .NET, but activities can. Here's how to run
an activity worker:

```csharp
using Temporalio.Client;
using Temporalio.Worker;
using MyNamespace;

// Create client
var client = await TemporalClient.ConnectAsync(new ()
{
    TargetHost = "localhost:7233",
    Namespace = "my-namespace",
});

// Create worker
using var worker = new TemporalWorker(client, new ()
{
    TaskQueue = "my-activity-queue",
    Activities = { MyActivities.MyActivity },
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

### Workflows

Workflows cannot yet be written in .NET but they can be defined.

#### Workflow Definition

Workflows are defined as classes or interfaces with a `[Workflow]` attribute. The entry point method for a workflow has
the `[WorkflowRun]` attribute. Methods for signals and queries have the `[WorkflowSignal]` and `[WorkflowQuery]`
attributes respectively. Here is an example of a workflow definition:

```csharp
using System.Threading.Tasks;
using Temporalio.Workflow;

public record GreetingInfo(string Salutation = "Hello", string Name = "<unknown>");

[Workflow]
public interface IGreetingWorkflow
{
    static readonly IGreetingWorkflow Ref = Refs.Create<IGreetingWorkflow>();

    [WorkflowRun]
    Task<string> RunAsync(GreetingInfo initialInfo);

    [WorkflowSignal]
    Task UpdateSalutation(string salutation);

    [WorkflowSignal]
    Task CompleteWithGreeting();

    [WorkflowQuery]
    string CurrentGreeting();
}
```

Notes about the above code:

* The workflow client needs the ability to reference these instance methods, but C# doesn't allow referencing instance
  methods without an instance. Therefore we add a readonly `Ref` instance which is a proxy instance just for method
  references.
  * This is backed by a dynamic proxy generator but method invocations should never be made on it. It is only for
    referencing methods.
  * This is technically not needed. Any way that the method can be referenced for a client is acceptable.
  * Source generators will provide an additional, alternative way to use workflows in a typed way in the future.
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

#### Workflow Task Scheduling

TODO(cretz): Document and try to prevent:

* Users should not use `Task.Run`, `ConfigureAwait(false)` or any other task call that does not use
  `TaskScheduler.Current`
  * This is kinda the opposite of [CA2008](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2008)
    but it's important not to move back on to the primary thread pool.
  * We will try to prevent this at runtime with TPL event source capturing and at compile time with analyzers.
* Users should not use `Task.Delay`, `Task.Wait`, or timeout-based `CancellationTokenSource` or anything that uses .NET
  timers
  * .NET does not allow us to intercept the timers. It forces them on a platform-specific `TimerQueue` which is
    non-deterministic.
  * We will try to prevent this at runtime with TPL event source capturing and at compile time with analyzers.

### Activities

#### Activity Definition

Activities are methods with the `[Activity]` annotation like so:

```csharp
namespace MyNamespace;

using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Temporalio.Activity;

public static class Activities
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
        timer.Elapsed += (sender, eventArgs) => ActivityContext.Current.Heartbeat();

        // Issue our HTTP call
        using var response = await client.GetAsync(url, ActivityContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ActivityContext.Current.CancellationToken);
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
* Activities can be defined on static as instance methods. They can even be lambdas or local methods, but rarely is this
  valuable since often an activity will be referenced by a workflow.
* Activities can be synchronous or asynchronous. If an activity returns a `Task`, that task is awaited on as part of the
  activity.

#### Activity Context

During activity execution, an async-local activity context is available via `ActivityContext.Current`. This will throw
if not currently in an activity context (which can be checked with `ActivityContext.HasCurrent`). It contains the
following important members:

* `Info` - Information about the activity.
* `Logger` - A logger scoped to the activity.
* `CancelReason` - If `CancellationToken` is cancelled, this will contain the reason.
* `CancellationToken` - Token cancelled when the activity is cancelled.
* `Hearbeat(object?...)` - Send a heartbeat from this activity.
* `WorkerShutdownToken` - Token cancelled on worker shutdown before the grace period + `CancellationToken` cancellation.

#### Activity Heartbeating and Cancellation

In order for a non-local activity to be notified of cancellation requests, it must invoke `ActivityContext.Heartbeat()`.
It is strongly recommended that all but the fastest executing activities call this function regularly.

In addition to obtaining cancellation information, heartbeats also support detail data that is persisted on the server
for retrieval during activity retry. If an activity calls `ActivityContext.Heartbeat(123)` and then fails and is
retried, `ActivityContext.Info.HeartbeatDetails` will contain the last detail payloads. A helper can be used to convert,
so `await ActivityContext.Info.HeartbeatDetailAtAsync<int>(0)` would give `123` on the next attempt.

Heartbeating has no effect on local activities.

#### Activity Worker Shutdown

An activity can react to a worker shutdown specifically.

Upon worker shutdown, `ActivityContext.WorkerShutdownToken` is cancelled. Then the worker will wait a grace period set
by the `GracefulShutdownTimeout` worker option (default as 0) before issuing actual cancellation to all still-running
activities via `ActivityContext.CancellationToken`.

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
