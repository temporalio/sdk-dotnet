# System.Diagnostics.Metrics Support

This extension adds an implementation of `Temporalio.Runtime.ICustomMetricMeter` for `System.Diagnostics.Metrics.Meter`
so internal SDK metrics can be forwarded as regular .NET metrics.

## Quick Start

Add the `Temporalio.Extensions.DiagnosticSource` package from
[NuGet](https://www.nuget.org/packages/Temporalio.Extensions.DiagnosticSource). For example, using the `dotnet` CLI:

    dotnet add package Temporalio.Extensions.DiagnosticSource

Now a `Temporalio.Runtime.TemporalRuntime` can be created with a .NET meter and a client can be created from that:

```csharp
using System.Diagnostics.Metrics;
using Temporalio.Client;
using Temporalio.Extensions.DiagnosticSource;
using Temporalio.Runtime;

// Create .NET meter
using var meter = new Meter("My.Meter");
// Can create MeterListener or OTel meter provider here...

// Create Temporal runtime with a custom metric meter for that meter
var runtime = new TemporalRuntime(new()
{
    Telemetry = new()
    {
        Metrics = new() { CustomMetricMeter = new CustomMetricMeter(meter) },
    },
});

// Create a client using that runtime
var client = await TemporalClient.ConnectAsync(
    new()
    {
        TargetHost = "my-temporal-host:7233",
        Namespace = "my-temporal-namespace",
        Runtime = runtime,
    });

// Now all metrics for the client will go through the .NET meter
```

This client can be used for the worker too which means that all metrics, including ones created during activity using
`ActivityExecutionContext.Current.MetricMeter` and during workflow using `Workflow.MetricMeter`, will be recorded on
the .NET meter.