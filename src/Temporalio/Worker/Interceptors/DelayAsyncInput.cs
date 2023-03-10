using System;
using System.Threading;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.DelayAsync" />.
    /// </summary>
    /// <param name="Delay">Delay duration.</param>
    /// <param name="CancellationToken">Optional cancellation token.</param>
    public record DelayAsyncInput(
        TimeSpan Delay,
        CancellationToken? CancellationToken);
}