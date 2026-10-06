using System;
using System.Collections.Generic;
using System.Threading;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.DelayAsync" />.
    /// </summary>
    /// <param name="Delay">Delay duration.</param>
    /// <param name="CancellationToken">Optional cancellation token.</param>
    /// <param name="Summary">Summary for the delay.</param>
    /// <param name="EventGroups">Event Groups from <see cref="DelayOptions.EventGroups" />.
    /// WARNING: Event Groups are experimental.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record DelayAsyncInput(
        TimeSpan Delay,
        CancellationToken? CancellationToken,
        string? Summary,
        IReadOnlyCollection<EventGroup>? EventGroups)
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DelayAsyncInput"/> class.
        /// </summary>
        /// <param name="options">Options.</param>
        internal DelayAsyncInput(DelayOptions options)
            : this(options.Delay, options.CancellationToken, options.Summary, options.EventGroups)
        {
        }
    }
}
