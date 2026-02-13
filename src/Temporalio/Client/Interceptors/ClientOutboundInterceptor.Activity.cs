using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
        /// <summary>
        /// Intercept start activity calls.
        /// </summary>
        /// <typeparam name="TResult">Result type of the activity. May be ValueTuple if
        /// unknown.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Handle for the activity.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
            StartActivityInput input) =>
            Next.StartActivityAsync<TResult>(input);

        /// <summary>
        /// Intercept describe activity calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Activity execution description.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task<ActivityExecutionDescription> DescribeActivityAsync(
            DescribeActivityInput input) =>
            Next.DescribeActivityAsync(input);

        /// <summary>
        /// Intercept cancel activity calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for acceptance of the cancel.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task CancelActivityAsync(CancelActivityInput input) =>
            Next.CancelActivityAsync(input);

        /// <summary>
        /// Intercept terminate activity calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for termination completion.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task TerminateActivityAsync(TerminateActivityInput input) =>
            Next.TerminateActivityAsync(input);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Intercept listing activities.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Async enumerator for the activities.</returns>
        /// <remarks>
        /// WARNING: Standalone activities are experimental.
        /// <para>
        /// This method only gets called by <see cref="ITemporalClient.ListActivitiesAsync"/>.
        /// It does not get called by <see cref="ITemporalClient.ListActivitiesPaginatedAsync"/>.
        /// This method is called before the first page is fetched. Afterwards, before each page fetched
        /// (including before the first page), <see cref="ListActivitiesPaginatedAsync"/> is called.
        /// </para>
        /// </remarks>
        public virtual IAsyncEnumerable<ActivityExecution> ListActivitiesAsync(
            ListActivitiesInput input) =>
            Next.ListActivitiesAsync(input);
#endif

        /// <summary>
        /// Intercept counting activities.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Count information for the activities.</returns>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task<ActivityExecutionCount> CountActivitiesAsync(
            CountActivitiesInput input) =>
            Next.CountActivitiesAsync(input);

#pragma warning disable CS1574 // ListActivitiesAsync does not exist in .Net Framework/Standard
        /// <summary>
        /// Intercept page fetch for list activities calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>A single page of query results.</returns>
        /// <remarks>
        /// WARNING: Standalone activities are experimental.
        /// <para>
        /// This method is called each time <see cref="ITemporalClient.ListActivitiesPaginatedAsync"/> is called.
        /// It also gets called for each page fetched when iterating the enumerable returned by <see cref="ITemporalClient.ListActivitiesAsync"/>.
        /// </para>
        /// </remarks>
#pragma warning restore CS1574
        public virtual Task<ActivityListPage> ListActivitiesPaginatedAsync(
            ListActivitiesPaginatedInput input) =>
            Next.ListActivitiesPaginatedAsync(input);
    }
}
