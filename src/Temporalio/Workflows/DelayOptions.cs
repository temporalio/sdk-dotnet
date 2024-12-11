using System;
using System.Threading;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for timers (i.e. <c>DelayAsync</c>).
    /// </summary>
    public class DelayOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DelayOptions"/> class.
        /// </summary>
        public DelayOptions()
            : this(TimeSpan.Zero)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayOptions"/> class.
        /// </summary>
        /// <param name="millisecondsDelay">See <see cref="Delay" />.</param>
        /// <param name="summary">See <see cref="Summary" />.</param>
        /// <param name="cancellationToken">See <see cref="CancellationToken" />.</param>
        public DelayOptions(int millisecondsDelay, string? summary = null, CancellationToken? cancellationToken = null)
            : this(TimeSpan.FromMilliseconds(millisecondsDelay), summary, cancellationToken)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayOptions"/> class.
        /// </summary>
        /// <param name="delay">See <see cref="Delay" />.</param>
        /// <param name="summary">See <see cref="Summary" />.</param>
        /// <param name="cancellationToken">See <see cref="CancellationToken" />.</param>
        public DelayOptions(TimeSpan delay, string? summary = null, CancellationToken? cancellationToken = null)
        {
            Delay = delay;
            Summary = summary;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets or sets the amount of time to sleep.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>delay</c> value can be <see cref="Timeout.Infinite" /> or
        /// <see cref="Timeout.InfiniteTimeSpan" /> but otherwise cannot be
        /// negative. A server-side timer is not created for infinite delays, so it is
        /// non-deterministic to change a timer to/from infinite from/to an actual value.
        /// </para>
        /// <para>
        /// If the <c>delay</c> is 0, it is assumed to be 1 millisecond and still results in a
        /// server-side timer. Since Temporal timers are server-side, timer resolution may not end
        /// up as precise as system timers.
        /// </para>
        /// </remarks>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// Gets or sets a simple string identifying this timer that may be visible in UI/CLI. While
        /// it can be normal text, it is best to treat as a timer ID.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? Summary { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token for the timer. If unset, this defaults to
        /// <see cref="Workflow.CancellationToken" />.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
