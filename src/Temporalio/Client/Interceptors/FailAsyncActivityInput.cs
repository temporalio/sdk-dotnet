using System;
using Temporalio.Converters;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.FailAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to fail.</param>
    /// <param name="Exception">Exception.</param>
    /// <param name="Options">Options passed in to fail.</param>
    /// <param name="DataConverterOverride">Data converter to use instead of client one.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record FailAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        Exception Exception,
        AsyncActivityFailOptions? Options,
        DataConverter? DataConverterOverride = null);
}