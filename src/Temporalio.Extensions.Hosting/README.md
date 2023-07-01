# Hosting and Dependency Injection Support

This extension adds support for worker
[Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)s and activity
[Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) to the
[Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet).

⚠️ UNDER ACTIVE DEVELOPMENT

This SDK is under active development and has not released a stable version yet. APIs may change in incompatible ways
until the SDK is marked stable.

## Quick Start

Add the `Temporalio.Extensions.Hosting` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.Hosting). For example, using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.Hosting --prerelease

To create a worker service, you can do the following:

```csharp
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
// Add a hosted Temporal worker which returns a builder to add activities and workflows
builder.Services.
    AddHostedTemporalWorker(
        "my-temporal-host:7233",
        "my-namespace",
        "my-task-queue").
    AddScopedActivities<MyActivityClass>().
    AddWorkflow<MyWorkflow>();

// Run
var host = builder.Build();
host.Run();
```

This creates a hosted Temporal worker which returns a builder. Then `MyActivityClass` is added to the service collection
as scoped (via
[`TryAddScoped`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.extensions.servicecollectiondescriptorextensions.tryaddscoped))
and registered on the worker. Also `MyWorkflow` is registered as a workflow on the worker.

This means that for every activity invocation, a new scope is created and the the activity is obtained via the service
provider. So if it is registered as scoped the activity is created each time but if it registered as singleton it is
created only once and reused. The activity's constructor can be used to accept injected dependencies.

Workflows are inherently self-contained, deterministic units of work and therefore should never call anything external.
Therefore, there is no such thing as dependency injection for workflows, their construction and lifetime is managed by
Temporal.

## Worker Services

When this extension is depended upon, two overloads for `AddHostedTemporalWorker` are added as extension methods on
`IServiceCollection` in the `Microsoft.Extensions.DependencyInjection`.

One overload of `AddHostedTemporalWorker`, used in the quick start sample above, accepts the client target host, the
client namespace, and the worker task queue. This form will connect to a client for the worker. The other overload of
`AddHostedTemporalWorker` only accepts the worker task queue. In the latter, an `ITemporalClient` or
`ITemporalClientProvider` can be set on the services container and therefore reused across workers, or the resulting
builder can have client options set.

When called, these register a `TemporalWorkerServiceOptions` options class with the container and return a
`ITemporalWorkerServiceOptionsBuilder` for configuring the worker service options.

For adding activity classes to the service collection and registering activities with the worker, the following
extension methods exist on the builder each accepting activity type:

* `AddSingletonActivities` - `TryAddSingleton` + register activities on worker
* `AddScopedActivities` - `TryAddScoped` + register activities on worker
* `AddTransientActivities` - `TryAddTransient` + register activities on worker
* `AddStaticActivities` - register activities on worker that all must be static
* `AddActivitiesInstance` - register activities on worker via an existing instance

These all expect the activity methods to have the `[Activity]` attribute on them. If an activity type is already added
to the service collection, `ApplyTemporalActivities` can be used to just register the activities on the worker.

For registering workflows on the worker, `AddWorkflow` extension method is available. This does nothing to the service
collection because the construction and lifecycle of workflows is managed by Temporal. Dependency injection for
workflows is intentionally not supported.

Other worker and client options can be configured on the builder via the `ConfigureOptions` extension method. With no
parameters, this returns an `OptionsBuilder<TemporalWorkerServiceOptions>` to use. When provided an action, the options
are available as parameters that can be configured. `TemporalWorkerServiceOptions` simply extends
`TemporalWorkerOptions` with an added property for optional client options that can be set to connect a client on worker
start instead of expecting a `ITemporalClient` to be present on the service collection.

## Activity Dependency Injection without Worker Services

Some users may prefer to manually create the `TemporalWorker` without using host support, but still make their
activities created via the service provider. `CreateTemporalActivityDefinitions` extension methods are present on
`IServiceProvider` that will return a collection of `ActivityDefinition` instances for each activity on the type. These
can be added to the `TemporalWorkerOptions` directly.