namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CompleteAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to complete.</param>
    /// <param name="Result">Result.</param>
    /// <param name="Options">Options passed in to complete.</param>
    public record CompleteAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        object? Result,
        AsyncActivityCompleteOptions? Options);
}