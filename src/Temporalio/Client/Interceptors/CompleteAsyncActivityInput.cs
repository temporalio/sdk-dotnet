using Temporalio.Converters;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CompleteAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to complete.</param>
    /// <param name="Result">Result.</param>
    /// <param name="Options">Options passed in to complete.</param>
    /// <param name="DataConverterOverride">Data converter to use instead of client one.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CompleteAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        object? Result,
        AsyncActivityCompleteOptions? Options,
        DataConverter? DataConverterOverride = null);
}