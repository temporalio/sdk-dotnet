using System;
using System.Threading.Tasks;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Outbound interceptor to intercept workflow calls coming from workflows.
    /// </summary>
    public abstract class WorkflowOutboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOutboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        protected WorkflowOutboundInterceptor(WorkflowOutboundInterceptor next) => MaybeNext = next;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOutboundInterceptor"/> class.
        /// </summary>
        private protected WorkflowOutboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected WorkflowOutboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected WorkflowOutboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Intercept cancel external workflow.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task CancelExternalWorkflowAsync(
            CancelExternalWorkflowInput input) => Next.CancelExternalWorkflowAsync(input);

        /// <summary>
        /// Intercept continue as new exception creation.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Created exception.</returns>
        public virtual ContinueAsNewException CreateContinueAsNewException(
            CreateContinueAsNewExceptionInput input) => Next.CreateContinueAsNewException(input);

        /// <summary>
        /// Intercept starting a new timer.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task DelayAsync(DelayAsyncInput input) => Next.DelayAsync(input);

        /// <summary>
        /// Intercept scheduling an activity.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Activity result.</returns>
        public virtual Task<TResult> ScheduleActivityAsync<TResult>(
            ScheduleActivityInput input) => Next.ScheduleActivityAsync<TResult>(input);

        /// <summary>
        /// Intercept scheduling a local activity.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Activity result.</returns>
        public virtual Task<TResult> ScheduleLocalActivityAsync<TResult>(
            ScheduleLocalActivityInput input) => Next.ScheduleLocalActivityAsync<TResult>(input);

        /// <summary>
        /// Intercept sending of a child workflow signal.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task SignalChildWorkflowAsync(
            SignalChildWorkflowInput input) => Next.SignalChildWorkflowAsync(input);

        /// <summary>
        /// Intercept sending of an external workflow signal.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task SignalExternalWorkflowAsync(
            SignalExternalWorkflowInput input) => Next.SignalExternalWorkflowAsync(input);

        /// <summary>
        /// Intercept starting a child workflow. To intercept other child workflow calls, the handle
        /// can be extended/customized.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Child handle.</returns>
        public virtual Task<ChildWorkflowHandle<TResult>> StartChildWorkflowAsync<TResult>(
            StartChildWorkflowInput input) => Next.StartChildWorkflowAsync<TResult>(input);
    }
}