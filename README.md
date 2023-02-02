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
* Samples (TODO)

⚠️ UNDER ACTIVE DEVELOPMENT

This SDK is under active development and has not released a stable version yet. APIs may change in incompatible ways
until the SDK is marked stable.

Notably missing from this SDK:

* Activity workers
* Workflow workers

(for the previous .NET SDK repo, see https://github.com/temporalio/experiment-dotnet)

## Quick Start

## Installation

Add the `Temporalio` package from [NuGet](https://www.nuget.org/packages/Temporalio). For example, using the `dotnet`
CLI:

    dotnet add package Temporalio --prerelease

**NOTE: This README is for the current branch and not necessarily what's released on NuGet.**

## Usage

### Workflow Definitions

The current SDK does not yet support workflows written in .NET, but workflows from other languages can still be defined
in .NET to be properly typed.

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
    IMyWorkflow.RunAsync,
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

Data converters are using to convert raw Temporal payloads to/from actual .NET types. A custom data converter can be set
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

### Workflows

Workflows cannot yet be written in .NET but they can be defined.

#### Definition

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
    static readonly IGreetingWorkflow Ref = Refs<IGreetingWorkflow>.Instance;

    [WorkflowRun]
    public Task<string> RunAsync(GreetingInfo initialInfo);

    [WorkflowSignal]
    public Task UpdateSalutation(string salutation);

    [WorkflowSignal]
    public Task CompleteWithGreeting();

    [WorkflowQuery]
    public string CurrentGreeting();
}
```

Notes about the above code:

* The workflow client needs the ability to reference these instance methods, but C# doesn't allow referencing instance
  methods without an instance. Therefore we add a readonly `Ref` instance which is a proxy instance just for method
  references.
  * This is backed by a dynamic proxy generator but method invocations should never be made on it. It is only a for
    referencing methods.
  * This is technically not needed. Any way that the method can be referenced for a client is acceptable.
  * Source generators will provide an additional, alternative way to use workflows in a typed way in the future.
* `[Workflow]` attribute must be present on the workflow type.
  * The attribute can have a string argument for the workflow type name. Otherwise the name is defaulted to the
    unqualified type name (with the `I` prefix removed if on an interface and has a capital letter following).
* `[WorkflowRun]` attribute must be present on one and only one public method.
  * The workflow run method must return a `Task`.
  * The workflow run method _should_ accept a single parameter and return a single type that are often srecord. Records
    are encouraged because optional fields can be added without affecting backwards compatibility of the workflow
    definition.
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

### Activities

Activities are not implemented yet.

## Development

### Build

With `dotnet` installed with all needed frameworks and Rust installed (i.e.
`cargo` on the `PATH`), run:

    dotnet build

Or for release:

    dotnet build --configuration Release

### Code formatting

This project uses StyleCop analyzers with some overrides in `.editorconfig`. To format, run:

    dotnet format

Can also run with `--verify-no-changes` to ensure it is formatted.

#### VisualStudio Code

When developing in vscode, the following JSON settings will enable StyleCop analyzers in:

```json
    "omnisharp.enableEditorConfigSupport": true,
    "omnisharp.enableRoslynAnalyzers": true
```

### Testing

Run:

    dotnet test

Can add options like:

* `--logger "console;verbosity=detailed"` to show logs
  * TODO(cretz): This doesn't show Rust stdout. How do I do that?
* `--filter "FullyQualifiedName=Temporalio.Tests.Client.TemporalClientTests.ConnectAsync_Connection_Succeeds"` to run a
  specific test
* `--blame-crash` to do a host process dump on crash

To help debug native pieces, this is also available as an in-proc test program. Run:

    dotnet run --project tests/Temporalio.Tests

Extra args can be added after `--`, e.g. `-- --verbose` would show verbose logs and `-- --help` would show other
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
