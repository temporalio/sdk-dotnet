using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Common;
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
        /// Gets or sets value for <see cref="Workflow.DynamicQuery" />.
        /// </summary>
        WorkflowQueryDefinition? DynamicQuery { get; set; }

        /// <summary>
        /// Gets or sets value for <see cref="Workflow.DynamicSignal" />.
        /// </summary>
        WorkflowSignalDefinition? DynamicSignal { get; set; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Info" />.
        /// </summary>
        WorkflowInfo Info { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Workflow.Unsafe.IsReplaying" /> is true.
        /// </summary>
        bool IsReplaying { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Logger" />.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Memo" />.
        /// </summary>
        IReadOnlyDictionary<string, IRawValue> Memo { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.PayloadConverter" />.
        /// </summary>
        IPayloadConverter PayloadConverter { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Queries" />.
        /// </summary>
        IDictionary<string, WorkflowQueryDefinition> Queries { get; }

        /// <summary>
        /// Gets value for <see cref="Workflow.Random" />.
        /// </summary>
        DeterministicRandom Random { get; }

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
        /// Backing call for
        /// <see cref="Workflow.CreateContinueAsNewException(string, IReadOnlyCollection{object?}, ContinueAsNewOptions?)" />.
        /// </summary>
        /// <param name="workflow">Workflow name.</param>
        /// <param name="args">Workflow args.</param>
        /// <param name="options">Options.</param>
        /// <returns>Continue as new exception.</returns>
        ContinueAsNewException CreateContinueAsNewException(
            string workflow, IReadOnlyCollection<object?> args, ContinueAsNewOptions? options);

        /// <summary>
        /// Backing call for <see cref="Workflow.DelayAsync(TimeSpan, CancellationToken?)" /> and
        /// overloads.
        /// </summary>
        /// <param name="delay">Delay duration.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Task for completion.</returns>
        Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken);

        /// <summary>
        /// Backing call for
        /// <see cref="Workflow.ExecuteActivityAsync{TResult}(string, IReadOnlyCollection{object?}, ActivityOptions)" />.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity name.</param>
        /// <param name="args">Activity args.</param>
        /// <param name="options">Options.</param>
        /// <returns>Activity result.</returns>
        Task<TResult> ExecuteActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, ActivityOptions options);

        /// <summary>
        /// Backing call for
        /// <see cref="Workflow.ExecuteLocalActivityAsync{TResult}(string, IReadOnlyCollection{object?}, LocalActivityOptions)" />.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity name.</param>
        /// <param name="args">Activity args.</param>
        /// <param name="options">Options.</param>
        /// <returns>Activity result.</returns>
        Task<TResult> ExecuteLocalActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, LocalActivityOptions options);

        /// <summary>
        /// Backing call for <see cref="Workflow.GetExternalWorkflowHandle{TWorkflow}(string, string?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="id">Workflow ID.</param>
        /// <param name="runID">Optional workflow run ID.</param>
        /// <returns>External workflow handle.</returns>
        ExternalWorkflowHandle<TWorkflow> GetExternalWorkflowHandle<TWorkflow>(
            string id, string? runID = null);

        /// <summary>
        /// Backing call for <see cref="Workflow.Patched" /> and
        /// <see cref="Workflow.DeprecatePatch" />.
        /// </summary>
        /// <param name="patchID">Patch ID.</param>
        /// <param name="deprecated">Whether to deprecate.</param>
        /// <returns>Whether patched.</returns>
        bool Patch(string patchID, bool deprecated);

        /// <summary>
        /// Backing call for
        /// <see cref="Workflow.StartChildWorkflowAsync(string, IReadOnlyCollection{object?}, ChildWorkflowOptions?)" />
        /// and overloads.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow name.</param>
        /// <param name="args">Workflow args.</param>
        /// <param name="options">Options.</param>
        /// <returns>Child workflow handle.</returns>
        Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions options);

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