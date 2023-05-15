using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Exceptions;
using Temporalio.Runtime;
using Temporalio.Worker.Interceptors;

#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Temporalio.Worker
{
    /// <summary>
    /// Replayer to replay workflows from existing history.
    /// </summary>
    public class WorkflowReplayer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowReplayer"/> class. Note, some
        /// options validation is deferred until replay is attempted.
        /// </summary>
        /// <param name="options">Replayer options.</param>
        public WorkflowReplayer(WorkflowReplayerOptions options)
        {
            if (options.Workflows.Count == 0)
            {
                throw new ArgumentException("Must have at least one workflow");
            }
            Options = options;
        }

        /// <summary>
        /// Gets the options this replayer was created with.
        /// </summary>
        public WorkflowReplayerOptions Options { get; private init; }

        /// <summary>
        /// Replay a single workflow from the given history.
        /// </summary>
        /// <param name="history">History to replay.</param>
        /// <param name="throwOnReplayFailure">If true, the default, this will throw
        /// <see cref="InvalidWorkflowOperationException" /> on a workflow task failure
        /// (e.g. non-determinism). This has no effect on workflow failures which are not reported
        /// as part of replay.</param>
        /// <returns>Result of the replay run.</returns>
        public async Task<WorkflowReplayResult> ReplayWorkflowAsync(
            WorkflowHistory history, bool throwOnReplayFailure = true) =>
            (await ReplayWorkflowsAsync(new[] { history }, throwOnReplayFailure).ConfigureAwait(false)).Single();

        /// <summary>
        /// Replay multiple workflows from the given histories.
        /// </summary>
        /// <param name="histories">Histories to replay.</param>
        /// <param name="throwOnReplayFailure">If true, which is not the default, this will throw
        /// <see cref="InvalidWorkflowOperationException" /> on a workflow task failure
        /// (e.g. non-determinism) as soon as it's encountered. This has no effect on workflow
        /// failures which are not reported as part of replay.</param>
        /// <returns>Results of the replay runs.</returns>
        public async Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            IEnumerable<WorkflowHistory> histories, bool throwOnReplayFailure = false)
        {
            // We could stream the results, but since the method wants them all anyways, a list is
            // ok
            using (var runner = new WorkflowHistoryRunner(Options, throwOnReplayFailure))
            using (var cts = new CancellationTokenSource())
            {
                var workerTask = Task.Run(() => runner.RunWorkerAsync(cts.Token));
                try
                {
                    var results = new List<WorkflowReplayResult>();
                    foreach (var history in histories)
                    {
                        results.Add(await runner.RunWorkflowAsync(history).ConfigureAwait(false));
                    }
                    return results;
                }
                finally
                {
                    // Cancel and wait on complete
                    cts.Cancel();
                    await workerTask.ConfigureAwait(false);
                }
            }
        }

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Replay multiple workflows from the given histories.
        /// </summary>
        /// <param name="histories">Histories to replay.</param>
        /// <param name="throwOnReplayFailure">If true, which is not the default, this will throw
        /// <see cref="InvalidWorkflowOperationException" /> on a workflow task failure
        /// (e.g. non-determinism) as soon as it's encountered. This has no effect on workflow
        /// failures which are not reported as part of replay.</param>
        /// <param name="cancellationToken">Cancellation token to stop replaying.</param>
        /// <returns>Results of the replay runs.</returns>
        public async IAsyncEnumerable<WorkflowReplayResult> ReplayWorkflowsAsync(
            IAsyncEnumerable<WorkflowHistory> histories,
            bool throwOnReplayFailure = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using (var runner = new WorkflowHistoryRunner(Options, throwOnReplayFailure))
            using (var shutdownTokenSource = new CancellationTokenSource())
            {
                var workerTask = Task.Run(
                    () => runner.RunWorkerAsync(shutdownTokenSource.Token), CancellationToken.None);
                try
                {
                    var results = new List<WorkflowReplayResult>();
                    await foreach (var history in histories)
                    {
                        // Run until complete or cancelled, throw if cancelled, yield if complete
                        var runTask = runner.RunWorkflowAsync(history);
                        await Task.WhenAny(
                            Task.Delay(Timeout.Infinite, cancellationToken),
                            runTask).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return await runTask.ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Cancel and wait on complete
                    shutdownTokenSource.Cancel();
                    await workerTask.ConfigureAwait(false);
                }
            }
        }
#endif

        /// <summary>
        /// Runner for workflow history.
        /// </summary>
        internal class WorkflowHistoryRunner : IDisposable
        {
            private readonly bool throwOnReplayFailure;
            private readonly Bridge.WorkerReplayer bridgeReplayer;
            private readonly WorkflowWorker workflowWorker;
            private WorkflowHistory? lastHistory;
            private TaskCompletionSource<WorkflowReplayResult>? pendingResult;

            /// <summary>
            /// Initializes a new instance of the <see cref="WorkflowHistoryRunner"/> class.
            /// </summary>
            /// <param name="options">Replayer options.</param>
            /// <param name="throwOnReplayFailure">Whether to throw on failure.</param>
            public WorkflowHistoryRunner(WorkflowReplayerOptions options, bool throwOnReplayFailure)
            {
                this.throwOnReplayFailure = throwOnReplayFailure;
                var runtime = options.Runtime ?? TemporalRuntime.Default;
                bridgeReplayer = new Bridge.WorkerReplayer(runtime.Runtime, options);
                try
                {
                    // Create workflow worker
                    var interceptorConstructorTypes = new Type[] { typeof(WorkflowInboundInterceptor) };
                    var interceptorTypes = options.Interceptors?.Select(
                        i =>
                        {
                            var type = i.WorkflowInboundInterceptorType;
                            if (type == null)
                            {
                                return null;
                            }
                            else if (type.GetConstructor(interceptorConstructorTypes) == null)
                            {
                                throw new InvalidOperationException(
                                    $"Workflow interceptor {type} missing constructor accepting inbound");
                            }
                            return type;
                        })?.OfType<Type>() ?? Enumerable.Empty<Type>();
                    workflowWorker = new WorkflowWorker(
                        new(
                            BridgeWorker: bridgeReplayer.Worker,
                            Namespace: options.Namespace,
                            TaskQueue: options.TaskQueue,
                            Workflows: options.Workflows,
                            DataConverter: options.DataConverter,
                            WorkflowInboundInterceptorTypes: interceptorTypes,
                            LoggerFactory: options.LoggerFactory,
                            WorkflowInstanceFactory: options.WorkflowInstanceFactory,
                            DebugMode: options.DebugMode,
                            DisableWorkflowTracingEventListener: options.DisableWorkflowTracingEventListener,
                            WorkflowStackTrace: WorkflowStackTrace.None),
                        (runID, removeFromCache) => SetResult(removeFromCache));
                }
                catch
                {
                    bridgeReplayer.Dispose();
                    throw;
                }
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="WorkflowHistoryRunner"/> class.
            /// </summary>
            ~WorkflowHistoryRunner() => Dispose(false);

            /// <inheritdoc/>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Run the worker. This won't return until worker is complete or cancelled.
            /// </summary>
            /// <param name="token">Token to cancel.</param>
            /// <returns>Task for completion.</returns>
            public async Task RunWorkerAsync(CancellationToken token)
            {
                using (token.Register(bridgeReplayer.Worker.InitiateShutdown))
                {
                    await workflowWorker.ExecuteAsync().ConfigureAwait(false);
                }
                await bridgeReplayer.Worker.FinalizeShutdownAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// Run a single workflow history.
            /// </summary>
            /// <param name="history">History to run.</param>
            /// <returns>Task for result.</returns>
            public Task<WorkflowReplayResult> RunWorkflowAsync(WorkflowHistory history)
            {
                lastHistory = history;
                pendingResult = new();
                bridgeReplayer.PushHistory(history.ID, new() { Events = { history.Events } });
                return pendingResult.Task;
            }

            /// <summary>
            /// Dispose the worker.
            /// </summary>
            /// <param name="disposing">Whether disposing.</param>
            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    bridgeReplayer.Worker.Dispose();
                    bridgeReplayer.Dispose();
                }
            }

            private void SetResult(RemoveFromCache removeFromCache)
            {
                Exception? failure = null;
                if (removeFromCache.Reason != RemoveFromCache.Types.EvictionReason.CacheFull &&
                    removeFromCache.Reason != RemoveFromCache.Types.EvictionReason.LangRequested)
                {
                    failure = new InvalidWorkflowOperationException(
                        $"{removeFromCache.Reason}: {removeFromCache.Message}");
                }
                if (failure != null && throwOnReplayFailure)
                {
                    pendingResult!.SetException(failure);
                }
                else
                {
                    pendingResult!.SetResult(new(lastHistory!, failure));
                }
            }
        }
    }
}