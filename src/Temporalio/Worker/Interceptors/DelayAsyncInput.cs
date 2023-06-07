using System;
using System.Threading;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.DelayAsync" />.
    /// </summary>
    /// <param name="Delay">Delay duration.</param>
    /// <param name="CancellationToken">Optional cancellation token.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record DelayAsyncInput(
        TimeSpan Delay,
        CancellationToken? CancellationToken);
}