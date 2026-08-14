namespace Temporalio.Tests.Extensions.OpenTelemetry;

using global::OpenTelemetry;
using global::OpenTelemetry.Trace;
using Xunit;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

public class OpenTelemetryConfigurationTests
{
    [Fact]
    public async Task ForceFlushAsync_RunsForceFlushOffCallerThread()
    {
        using var flushStarted = new ManualResetEventSlim();
        using var releaseFlush = new ManualResetEventSlim();
#pragma warning disable CA2000 // Tracer provider owns the processor/exporter.
        using var provider = Sdk.CreateTracerProviderBuilder().
            AddProcessor(new SimpleActivityExportProcessor(
                new BlockingForceFlushExporter(flushStarted, releaseFlush))).
            Build();
#pragma warning restore CA2000

#pragma warning disable CA2025 // The task is completed before the provider exits scope.
        var flushTask = TemporalOpenTelemetry.OpenTelemetryConfiguration.ForceFlushAsync(
            provider,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);
#pragma warning restore CA2025

        try
        {
            Assert.True(flushStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(flushTask.IsCompleted);
        }
        finally
        {
            releaseFlush.Set();
            await flushTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task ForceFlushAsync_ReturnsWhenCancellationRequested()
    {
        using var flushStarted = new ManualResetEventSlim();
        using var releaseFlush = new ManualResetEventSlim();
        using var flushCompleted = new ManualResetEventSlim();
#pragma warning disable CA2000 // Tracer provider owns the processor/exporter.
        using var provider = Sdk.CreateTracerProviderBuilder().
            AddProcessor(new SimpleActivityExportProcessor(
                new BlockingForceFlushExporter(flushStarted, releaseFlush, flushCompleted))).
            Build();
#pragma warning restore CA2000
        using var cts = new CancellationTokenSource();

#pragma warning disable CA2025 // The provider exits scope after the blocking flush is released.
        var flushTask = TemporalOpenTelemetry.OpenTelemetryConfiguration.ForceFlushAsync(
            provider,
            TimeSpan.FromSeconds(10),
            cts.Token);
#pragma warning restore CA2025

        try
        {
            Assert.True(flushStarted.Wait(TimeSpan.FromSeconds(5)));
            await cts.CancelAsync();
            await flushTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(flushCompleted.IsSet);
        }
        finally
        {
            releaseFlush.Set();
            Assert.True(flushCompleted.Wait(TimeSpan.FromSeconds(5)));
        }
    }
}
