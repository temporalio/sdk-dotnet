using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Start a standalone activity via lambda invoking the activity method.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activityCall">Invocation of activity method with a result.</param>
        /// <param name="options">Activity options. ID and TaskQueue are required.</param>
        /// <returns>Activity handle for the started activity.</returns>
        /// <exception cref="ArgumentException">Invalid activity call or options.</exception>
        /// <exception cref="Exceptions.ActivityAlreadyStartedException">
        /// Activity was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
            Expression<Func<Task<TResult>>> activityCall, StartActivityOptions options);

        /// <summary>
        /// Start a standalone activity via lambda invoking the activity method with no result.
        /// </summary>
        /// <param name="activityCall">Invocation of activity method with no result.</param>
        /// <param name="options">Activity options. ID and TaskQueue are required.</param>
        /// <returns>Activity handle for the started activity.</returns>
        /// <exception cref="ArgumentException">Invalid activity call or options.</exception>
        /// <exception cref="Exceptions.ActivityAlreadyStartedException">
        /// Activity was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        Task<ActivityHandle> StartActivityAsync(
            Expression<Func<Task>> activityCall, StartActivityOptions options);

        /// <summary>
        /// Start a standalone activity by name.
        /// </summary>
        /// <param name="activity">Activity type name.</param>
        /// <param name="args">Arguments for the activity.</param>
        /// <param name="options">Activity options. ID and TaskQueue are required.</param>
        /// <returns>Activity handle for the started activity.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.ActivityAlreadyStartedException">
        /// Activity was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        Task<ActivityHandle> StartActivityAsync(
            string activity, IReadOnlyCollection<object?> args, StartActivityOptions options);

        /// <summary>
        /// Get a handle for an existing standalone activity with unknown result type.
        /// </summary>
        /// <param name="id">Activity ID.</param>
        /// <param name="runId">Activity run ID or null for latest.</param>
        /// <returns>Created activity handle.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        ActivityHandle GetActivityHandle(string id, string? runId = null);

        /// <summary>
        /// Get a handle for an existing standalone activity with known result type.
        /// </summary>
        /// <typeparam name="TResult">Result type of the activity.</typeparam>
        /// <param name="id">Activity ID.</param>
        /// <param name="runId">Activity run ID or null for latest.</param>
        /// <returns>Created activity handle.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        ActivityHandle<TResult> GetActivityHandle<TResult>(string id, string? runId = null);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// List activities with the given query.
        /// </summary>
        /// <param name="query">Query to use for filtering.</param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>Async enumerator for the activities.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public IAsyncEnumerable<ActivityExecution> ListActivitiesAsync(
            string query, ActivityListOptions? options = null);
#endif

        /// <summary>
        /// Count activities with the given query.
        /// </summary>
        /// <param name="query">Query to use for counting.</param>
        /// <param name="options">Options for the count call.</param>
        /// <returns>Count information for the activities.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public Task<ActivityExecutionCount> CountActivitiesAsync(
            string query, ActivityCountOptions? options = null);

        /// <summary>
        /// List activities with the given query using manual paging.
        /// </summary>
        /// <param name="query">
        /// Query to use for filtering. Subsequent pages must have the same query as the initial
        /// call.
        /// </param>
        /// <param name="nextPageToken">
        /// Set to null for the initial call to retrieve the first page.
        /// Set to <see cref="ActivityListPage.NextPageToken"/> returned by a previous call to
        /// retrieve the next page.
        /// </param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>
        /// A single page of a list of activities.
        /// Repeat the call using <see cref="ActivityListPage.NextPageToken"/> to get more pages.
        /// </returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        Task<ActivityListPage> ListActivitiesPaginatedAsync(
            string query, byte[]? nextPageToken, ActivityListPaginatedOptions? options = null);
    }
}
