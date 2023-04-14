using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Client.Interceptors;

namespace Temporalio.Testing
{
    /// <summary>
    /// Interceptor to auto-time-skip when waiting on workflow result.
    /// </summary>
    internal class TimeSkippingClientInterceptor : IClientInterceptor
    {
        private readonly WorkflowEnvironment.EphemeralServerBased env;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSkippingClientInterceptor"/> class.
        /// </summary>
        /// <param name="env">Workflow environment.</param>
        public TimeSkippingClientInterceptor(WorkflowEnvironment.EphemeralServerBased env) =>
            this.env = env;

        /// <inheritdoc />
        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
            new TimeSkippingClientOutboundInterceptor(next, env);

        /// <summary>
        /// Intercept start workflow calls to alter the workflow handle.
        /// </summary>
        internal class TimeSkippingClientOutboundInterceptor : ClientOutboundInterceptor
        {
            private readonly WorkflowEnvironment.EphemeralServerBased env;

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="TimeSkippingClientOutboundInterceptor"/> class.
            /// </summary>
            /// <param name="next">Next interceptor.</param>
            /// <param name="env">Workflow environment.</param>
            public TimeSkippingClientOutboundInterceptor(
                ClientOutboundInterceptor next, WorkflowEnvironment.EphemeralServerBased env)
                : base(next) => this.env = env;

            /// <inheritdoc />
            public override async Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(StartWorkflowInput input) =>
                new TimeSkippingWorkflowHandle<TResult>(
                    env, await base.StartWorkflowAsync<TResult>(input).ConfigureAwait(false));
        }

        internal record TimeSkippingWorkflowHandle<TResult> : WorkflowHandle<TResult>
        {
            private readonly WorkflowEnvironment.EphemeralServerBased env;

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="TimeSkippingWorkflowHandle{TResult}"/> class.
            /// </summary>
            /// <param name="env">Workflow environment.</param>
            /// <param name="underlying">Underlying handle.</param>
            public TimeSkippingWorkflowHandle(
                WorkflowEnvironment.EphemeralServerBased env,
                WorkflowHandle<TResult> underlying)
                : base(
                    Client: underlying.Client,
                    ID: underlying.ID,
                    RunID: underlying.RunID,
                    ResultRunID: underlying.ResultRunID,
                    FirstExecutionRunID: underlying.FirstExecutionRunID) => this.env = env;

            /// <inheritdoc />
            public override Task<TLocalResult> GetResultAsync<TLocalResult>(
                bool followRuns = true, RpcOptions? rpcOptions = null) =>
                env.WithTimeSkippingUnlockedAsync(
                    () => base.GetResultAsync<TLocalResult>(followRuns, rpcOptions));
        }
    }
}