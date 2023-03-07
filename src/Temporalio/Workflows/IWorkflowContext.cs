using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Converters;

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
        /// Gets value for <see cref="Workflow.Memo" />.
        /// </summary>
        IReadOnlyDictionary<string, IRawValue> Memo { get; }

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
        /// Gets value for <see cref="Workflow.TypedSearchAttributes" />.
        /// </summary>
        SearchAttributeCollection TypedSearchAttributes { get; }

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
        /// Backing call for <see cref="Workflow.UpsertMemo" />.
        /// </summary>
        /// <param name="updates">Updates to perform.</param>
        void UpsertMemo(IReadOnlyCollection<MemoUpdate> updates);

        /// <summary>
        /// Backing call for <see cref="Workflow.UpsertTypedSearchAttributes" />.
        /// </summary>
        /// <param name="updates">Updates to perform.</param>
        void UpsertTypedSearchAttributes(IReadOnlyCollection<SearchAttributeUpdate> updates);

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