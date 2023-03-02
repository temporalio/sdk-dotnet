using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Context that is used by <see cref="Workflow" />.
    /// </summary>
    internal interface IWorkflowContext
    {
        /// <summary>
        /// Gets value for <see cref="Workflow.CancellationToken" />.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Info" />.
        /// </summary>
        WorkflowInfo Info { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Workflow.Unsafe.IsReplaying" /> is true.
        /// </summary>
        bool IsReplaying { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Queries" />.
        /// </summary>
        IDictionary<string, WorkflowQueryDefinition> Queries { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Random" />.
        /// </summary>
        Random Random { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Signals" />.
        /// </summary>
        IDictionary<string, WorkflowSignalDefinition> Signals { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.UtcNow" />.
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// Backing call for <see cref="Workflow.DelayAsync(TimeSpan, CancellationToken?)" /> and
        /// overloads.
        /// </summary>
        /// <param name="delay">Delay duration.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Task for completion.</returns>
        Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken);

        /// <summary>
        /// Backing call for <see cref="Workflow.WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" />
        /// and overloads.
        /// </summary>
        /// <param name="conditionCheck">Function to call.</param>
        /// <param name="timeout">Optional timeout.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Task for completion.</returns>
        Task<bool> WaitConditionAsync(
            Func<bool> conditionCheck,
            TimeSpan? timeout,
            CancellationToken? cancellationToken);
    }
}