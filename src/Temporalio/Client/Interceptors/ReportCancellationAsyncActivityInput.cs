using Temporalio.Converters;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ReportCancellationAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to report cancellation.</param>
    /// <param name="Options">Options passed in to report cancellation.</param>
    /// <param name="DataConverterOverride">Data converter to use instead of client one.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ReportCancellationAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        AsyncActivityReportCancellationOptions? Options,
        DataConverter? DataConverterOverride = null);
}