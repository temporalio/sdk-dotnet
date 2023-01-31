using System;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.FailAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to fail.</param>
    /// <param name="Exception">Exception.</param>
    /// <param name="Options">Options passed in to fail.</param>
    public record FailAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        Exception Exception,
        AsyncActivityFailOptions? Options);
}