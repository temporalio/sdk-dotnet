namespace Temporalio.Tests.Extensions.OpenTelemetry;

using global::OpenTelemetry;

internal sealed class BlockingForceFlushExporter : BaseExporter<System.Diagnostics.Activity>
{
    private readonly ManualResetEventSlim flushStarted;
    private readonly ManualResetEventSlim releaseFlush;
    private readonly ManualResetEventSlim? flushCompleted;

    public BlockingForceFlushExporter(
        ManualResetEventSlim flushStarted,
        ManualResetEventSlim releaseFlush,
        ManualResetEventSlim? flushCompleted = null)
    {
        this.flushStarted = flushStarted;
        this.releaseFlush = releaseFlush;
        this.flushCompleted = flushCompleted;
    }

    public override ExportResult Export(in Batch<System.Diagnostics.Activity> batch) =>
        ExportResult.Success;

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        flushStarted.Set();
        try
        {
            return releaseFlush.Wait(timeoutMilliseconds);
        }
        finally
        {
            flushCompleted?.Set();
        }
    }
}
