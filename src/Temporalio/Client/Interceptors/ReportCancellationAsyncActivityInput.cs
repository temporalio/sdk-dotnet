namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ReportCancellationAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to report cancellation.</param>
    /// <param name="Options">Options passed in to report cancellation.</param>
    public record ReportCancellationAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        AsyncActivityReportCancellationOptions? Options);
}