using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
        /// <summary>
        /// Intercept start Nexus operation calls.
        /// </summary>
        /// <typeparam name="TResult">Result type of the operation. May be ValueTuple if
        /// unknown.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Handle for the operation.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            StartNexusOperationInput input) =>
            Next.StartNexusOperationAsync<TResult>(input);

        /// <summary>
        /// Intercept describe Nexus operation calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Nexus operation execution description.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task<NexusOperationExecutionDescription> DescribeNexusOperationAsync(
            DescribeNexusOperationInput input) =>
            Next.DescribeNexusOperationAsync(input);

        /// <summary>
        /// Intercept cancel Nexus operation calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for acceptance of the cancel.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task CancelNexusOperationAsync(CancelNexusOperationInput input) =>
            Next.CancelNexusOperationAsync(input);

        /// <summary>
        /// Intercept terminate Nexus operation calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for termination completion.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task TerminateNexusOperationAsync(TerminateNexusOperationInput input) =>
            Next.TerminateNexusOperationAsync(input);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Intercept listing Nexus operations.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Async enumerator for the operations.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual IAsyncEnumerable<NexusOperationExecution> ListNexusOperationsAsync(
            ListNexusOperationsInput input) =>
            Next.ListNexusOperationsAsync(input);
#endif

        /// <summary>
        /// Intercept counting Nexus operations.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Count information for the operations.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task<NexusOperationExecutionCount> CountNexusOperationsAsync(
            CountNexusOperationsInput input) =>
            Next.CountNexusOperationsAsync(input);

#pragma warning disable CS1574 // ListNexusOperationsAsync does not exist in .Net Framework/Standard
        /// <summary>
        /// Intercept page fetch for list Nexus operations calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>A single page of query results.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
#pragma warning restore CS1574
        public virtual Task<NexusOperationListPage> ListNexusOperationsPaginatedAsync(
            ListNexusOperationsPaginatedInput input) =>
            Next.ListNexusOperationsPaginatedAsync(input);
    }
}
