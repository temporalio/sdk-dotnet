#pragma warning disable SA1402 // We are ok with two types of the same name in the same file

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow start operation to be used in
    /// <see cref="WorkflowStartUpdateWithStartOptions"/> and
    /// <see cref="WorkflowUpdateWithStartOptions"/> for update with start calls.
    /// </summary>
    public abstract class WithStartWorkflowOperation : ICloneable
    {
        // Atomic integer used to check if used yet (0 if unused, 1 if used)
        private int used;

        /// <summary>
        /// Initializes a new instance of the <see cref="WithStartWorkflowOperation"/> class.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Workflow options.</param>
        internal WithStartWorkflowOperation(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
            Workflow = workflow;
            Args = args;
            Options = options;
        }

        /// <summary>
        /// Gets or sets the workflow type to start.
        /// </summary>
        public string Workflow { get; set; }

        /// <summary>
        /// Gets or sets the workflow arguments.
        /// </summary>
        public IReadOnlyCollection<object?> Args { get; set; }

        /// <summary>
        /// Gets or sets the workflow options.
        /// </summary>
        public WorkflowOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the workflow headers.
        /// </summary>
        /// <remarks>
        /// NOTE: Some interceptors may mutate the dictionary.
        /// </remarks>
#pragma warning disable CA2227 // We are ok allowing this field to be mutated as a whole
        public IDictionary<string, Payload>? Headers { get; set; }
#pragma warning restore CA2227

        /// <summary>
        /// Gets the workflow handle type.
        /// </summary>
        internal abstract Type HandleType { get; }

        /// <summary>
        /// Gets a value indicating whether or not the result or exception has been set.
        /// </summary>
        internal abstract bool IsCompleted { get; }

        /// <summary>
        /// Create a start workflow operation via lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. <c>Id</c>, <c>TaskQueue</c>, and
        /// <c>IdConflictPolicy</c> are required. <c>StartSignal</c>, <c>StartSignalArgs</c>,
        /// <c>RequestEagerStart</c>, and <c>Rpc</c> are disallowed.</param>
        /// <returns>Start workflow operation.</returns>
        public static WithStartWorkflowOperation<WorkflowHandle<TWorkflow, TResult>> Create<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return new(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Create a start workflow operation via lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method.</param>
        /// <param name="options">Start workflow options. <c>Id</c>, <c>TaskQueue</c>, and
        /// <c>IdConflictPolicy</c> are required. <c>StartSignal</c>, <c>StartSignalArgs</c>,
        /// <c>RequestEagerStart</c>, and <c>Rpc</c> are disallowed.</param>
        /// <returns>Start workflow operation.</returns>
        public static WithStartWorkflowOperation<WorkflowHandle<TWorkflow>> Create<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return new(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Create a start workflow operation by name.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. <c>Id</c>, <c>TaskQueue</c>, and
        /// <c>IdConflictPolicy</c> are required. <c>StartSignal</c>, <c>StartSignalArgs</c>,
        /// <c>RequestEagerStart</c>, and <c>Rpc</c> are disallowed.</param>
        /// <returns>Start workflow operation.</returns>
        public static WithStartWorkflowOperation<WorkflowHandle> Create(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options) =>
            new(workflow, args, options);

        /// <summary>
        /// Create a shallow copy of this operation.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields. But it will
        /// not clone the arguments, headers, nor will it clone the underlying promise that is
        /// fulfilled when the workflow has started.
        /// </returns>
        public abstract object Clone();

        /// <summary>
        /// If not used, mark as such and return true. Otherwise, return false.
        /// </summary>
        /// <returns>True if marked used, false if already done before.</returns>
        internal bool TryMarkUsed() => Interlocked.CompareExchange(ref used, 1, 0) == 0;

        /// <summary>
        /// Set a successful workflow handle result.
        /// </summary>
        /// <param name="result">Workflow handle.</param>
        internal abstract void SetResult(WorkflowHandle result);

        /// <summary>
        /// Set a failure to start workflow.
        /// </summary>
        /// <param name="exception">Failure to start workflow.</param>
        internal abstract void SetException(Exception exception);
    }

    /// <summary>
    /// Representation of a workflow start operation to be used in
    /// <see cref="WorkflowStartUpdateWithStartOptions"/> and
    /// <see cref="WorkflowUpdateWithStartOptions"/> for update with start calls.
    /// </summary>
    /// <typeparam name="THandle">Workflow handle type.</typeparam>
    public sealed class WithStartWorkflowOperation<THandle> : WithStartWorkflowOperation
    where THandle : WorkflowHandle
    {
        private readonly TaskCompletionSource<THandle> handleCompletionSource = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WithStartWorkflowOperation{THandle}"/> class.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Workflow options.</param>
        internal WithStartWorkflowOperation(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
            : this(workflow, args, options, new())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WithStartWorkflowOperation{THandle}"/> class.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Workflow options.</param>
        /// <param name="handleCompletionSource">Task completion source for the handle.</param>
        internal WithStartWorkflowOperation(
            string workflow,
            IReadOnlyCollection<object?> args,
            WorkflowOptions options,
            TaskCompletionSource<THandle> handleCompletionSource)
            : base(workflow, args, options)
        {
            this.handleCompletionSource = handleCompletionSource;
        }

        /// <inheritdoc/>
        internal override Type HandleType => typeof(THandle);

        /// <inheritdoc/>
        internal override bool IsCompleted => handleCompletionSource.Task.IsCompleted;

        /// <inheritdoc/>
        public override object Clone() => new WithStartWorkflowOperation<THandle>(
            workflow: Workflow,
            args: Args,
            options: (WorkflowOptions)Options.Clone(),
            handleCompletionSource: handleCompletionSource);

        /// <summary>
        /// Get the started workflow handle, waiting if needed. The method call directly will never
        /// fail, but the task may with exceptions documented below.
        /// </summary>
        /// <returns>Task for the successfully completed handle.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public Task<THandle> GetHandleAsync() => handleCompletionSource.Task;

        /// <inheritdoc/>
        internal override void SetResult(WorkflowHandle result) =>
            handleCompletionSource.SetResult((THandle)result);

        /// <inheritdoc/>
        internal override void SetException(Exception exception) =>
            handleCompletionSource.SetException(exception);
    }
}
