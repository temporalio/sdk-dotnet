using Temporalio.Client;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Extension of <see cref="TemporalWorkerOptions" /> for Temporal worker service that also
    /// includes optional client connection options.
    /// </summary>
    public class TemporalWorkerServiceOptions : TemporalWorkerOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerServiceOptions"/> class.
        /// </summary>
        public TemporalWorkerServiceOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerServiceOptions"/> class.
        /// </summary>
        /// <param name="taskQueue">Task queue for the worker.</param>
        public TemporalWorkerServiceOptions(string taskQueue)
            : base(taskQueue)
        {
        }

        /// <summary>
        /// Gets or sets the client options. If set, the client will be connected on worker start.
        /// If not set, the worker service will expect an existing client to be present.
        /// </summary>
        public TemporalClientConnectOptions? ClientOptions { get; set; }

        /// <inheritdoc />
        public override object Clone()
        {
            var options = (TemporalWorkerServiceOptions)base.Clone();
            if (ClientOptions != null)
            {
                options.ClientOptions = (TemporalClientConnectOptions)ClientOptions.Clone();
            }
            return options;
        }

        /// <summary>
        /// Get an options name for the given task queue and build ID.
        /// </summary>
        /// <param name="taskQueue">Task queue.</param>
        /// <param name="buildId">Build ID.</param>
        /// <returns>Unique string name for the options.</returns>
        internal static string GetUniqueOptionsName(string taskQueue, string? buildId)
        {
            if (buildId == null)
            {
                return taskQueue;
            }
            return taskQueue + "!!__temporal__!!" + buildId;
        }
    }
}