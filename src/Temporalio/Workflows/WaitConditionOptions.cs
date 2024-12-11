using System;
using System.Threading;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for wait conditions.
    /// </summary>
    public class WaitConditionOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WaitConditionOptions"/> class.
        /// </summary>
        public WaitConditionOptions()
            : this(() => false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WaitConditionOptions"/> class.
        /// </summary>
        /// <param name="conditionCheck">See <see cref="ConditionCheck" />.</param>
        /// <param name="timeout">See <see cref="Timeout" />.</param>
        /// <param name="timeoutSummary">See <see cref="TimeoutSummary" />.</param>
        /// <param name="cancellationToken">See <see cref="CancellationToken" />.</param>
        public WaitConditionOptions(
            Func<bool> conditionCheck,
            TimeSpan? timeout = null,
            string? timeoutSummary = null,
            CancellationToken? cancellationToken = null)
        {
            ConditionCheck = conditionCheck;
            Timeout = timeout;
            TimeoutSummary = timeoutSummary;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets or sets the condition function.
        /// </summary>
        /// <remarks>
        /// This function is invoked on each iteration of the event loop. Therefore, it should be
        /// fast and side-effect free.
        /// </remarks>
        public Func<bool> ConditionCheck { get; set; }

        /// <summary>
        /// Gets or sets an optional timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets a simple string identifying the timer (created if <see cref="Timeout"/> is
        /// present) that may be visible in UI/CLI. While it can be normal text, it is best to treat
        /// as a timer ID.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? TimeoutSummary { get; set; }

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
