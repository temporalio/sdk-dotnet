using System;
using System.Threading.Tasks;
using NexusRpc.Handlers;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Inbound interceptor to intercept Nexus operation calls coming from the server.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public abstract class NexusOperationInboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationInboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        protected NexusOperationInboundInterceptor(NexusOperationInboundInterceptor next) =>
            MaybeNext = next;

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationInboundInterceptor"/> class.
        /// </summary>
        private protected NexusOperationInboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected NexusOperationInboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected NexusOperationInboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Intercept Nexus operation start.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completed operation start result.</returns>
        public virtual Task<OperationStartResult<object?>> ExecuteNexusOperationStartAsync(
            ExecuteNexusOperationStartInput input) => Next.ExecuteNexusOperationStartAsync(input);

        /// <summary>
        /// Intercept Nexus operation cancel.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completed task.</returns>
        public virtual Task ExecuteNexusOperationCancelAsync(
            ExecuteNexusOperationCancelInput input) => Next.ExecuteNexusOperationCancelAsync(input);
    }
}