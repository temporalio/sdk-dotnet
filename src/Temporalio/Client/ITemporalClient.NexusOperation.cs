using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Create a Nexus client for the given service name and options.
        /// </summary>
        /// <param name="service">Nexus service name.</param>
        /// <param name="options">Client options including endpoint.</param>
        /// <returns>Nexus client for the service.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        NexusClient CreateNexusClient(string service, NexusClientOptions options);

        /// <summary>
        /// Create a typed Nexus client for the given service type.
        /// </summary>
        /// <typeparam name="TService">Nexus service type.</typeparam>
        /// <param name="options">Client options including endpoint.</param>
        /// <returns>Typed Nexus client for the service.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        NexusClient<TService> CreateNexusClient<TService>(NexusClientOptions options);

        /// <summary>
        /// Create a typed Nexus client for the given service type and endpoint.
        /// </summary>
        /// <typeparam name="TService">Nexus service type.</typeparam>
        /// <param name="endpoint">Endpoint name.</param>
        /// <returns>Typed Nexus client for the service.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        NexusClient<TService> CreateNexusClient<TService>(string endpoint);

        /// <summary>
        /// Get a handle for an existing standalone Nexus operation with unknown result type.
        /// </summary>
        /// <param name="operationId">Operation ID.</param>
        /// <param name="operationRunId">Operation run ID or null for latest.</param>
        /// <returns>Created operation handle.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        NexusOperationHandle GetNexusOperationHandle(string operationId, string? operationRunId = null);

        /// <summary>
        /// Get a handle for an existing standalone Nexus operation with known result type.
        /// </summary>
        /// <typeparam name="TResult">Result type of the operation.</typeparam>
        /// <param name="operationId">Operation ID.</param>
        /// <param name="operationRunId">Operation run ID or null for latest.</param>
        /// <returns>Created operation handle.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        NexusOperationHandle<TResult> GetNexusOperationHandle<TResult>(string operationId, string? operationRunId = null);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// List Nexus operations with the given query.
        /// </summary>
        /// <param name="query">Query to use for filtering.</param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>Async enumerator for the operations.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public IAsyncEnumerable<NexusOperationExecution> ListNexusOperationsAsync(
            string query, NexusOperationListOptions? options = null);
#endif

        /// <summary>
        /// Count Nexus operations with the given query.
        /// </summary>
        /// <param name="query">Query to use for counting.</param>
        /// <param name="options">Options for the count call.</param>
        /// <returns>Count information for the operations.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public Task<NexusOperationExecutionCount> CountNexusOperationsAsync(
            string query, NexusOperationCountOptions? options = null);

        /// <summary>
        /// List Nexus operations with the given query using manual paging.
        /// </summary>
        /// <param name="query">
        /// Query to use for filtering. Subsequent pages must have the same query as the initial
        /// call.
        /// </param>
        /// <param name="nextPageToken">
        /// Set to null for the initial call to retrieve the first page.
        /// Set to <see cref="NexusOperationListPage.NextPageToken"/> returned by a previous call to
        /// retrieve the next page.
        /// </param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>
        /// A single page of a list of operations.
        /// Repeat the call using <see cref="NexusOperationListPage.NextPageToken"/> to get more pages.
        /// </returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        Task<NexusOperationListPage> ListNexusOperationsPaginatedAsync(
            string query, byte[]? nextPageToken, NexusOperationListPaginatedOptions? options = null);
    }
}
