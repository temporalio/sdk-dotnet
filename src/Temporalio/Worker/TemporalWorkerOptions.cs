using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Worker
{
    /// <summary>
    /// Options for a <see cref="TemporalWorker" />. <see cref="TaskQueue" /> and at least one
    /// workflow or activity are required.
    /// </summary>
    public class TemporalWorkerOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerOptions"/> class.
        /// </summary>
        public TemporalWorkerOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerOptions"/> class.
        /// </summary>
        /// <param name="taskQueue">Task queue for the worker.</param>
        public TemporalWorkerOptions(string taskQueue)
        {
            TaskQueue = taskQueue;
        }

        /// <summary>
        /// Gets or sets the task queue for the worker.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the task queue for activities. Default is <see cref="Task.Factory" />.
        /// </summary>
        public TaskFactory ActivityTaskFactory { get; set; } = Task.Factory;

#pragma warning disable CA2227 // Intentionally allow setting of this collection w/ options pattern
        /// <summary>
        /// Gets or sets the activity delegates.
        /// </summary>
        public IList<Delegate> Activities { get; set; } = new List<Delegate>();
#pragma warning restore CA2227

        /// <summary>
        /// Gets or sets the interceptors. Note this automatically includes any
        /// <see cref="Client.TemporalClientOptions.Interceptors" /> that implement
        /// <see cref="Interceptors.IWorkerInterceptor" /> so those should not be specified here.
        /// This set is chained after the set in the client options.
        /// </summary>
        public IEnumerable<Interceptors.IWorkerInterceptor>? Interceptors { get; set; }

        /// <summary>
        /// Gets or sets the build ID. This is a unique identifier for each "build" of the worker.
        /// If unset, the default is
        /// <c>Assembly.GetEntryAssembly().ManifestModule.ModuleVersionId</c>.
        /// </summary>
        public string? BuildID { get; set; }

        /// <summary>
        /// Gets or sets the identity for this worker. If unset, defaults to the client's identity.
        /// </summary>
        public string? Identity { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activities that will ever be given to this worker
        /// concurrently.
        /// </summary>
        public int MaxConcurrentActivities { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum number of concurrent poll activity task requests we will
        /// perform at a time on this worker's task queue.
        /// </summary>
        public int MaxConcurrentActivityTaskPolls { get; set; } = 5;

        /// <summary>
        /// Gets or sets the longest interval for throttling activity heartbeats.
        /// </summary>
        public TimeSpan MaxHeartbeatThrottleInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the default interval for throttling activity heartbeats in case
        /// per-activity heartbeat timeout is unset. Otherwise, it's the per-activity heartbeat
        /// timeout * 0.8.
        /// </summary>
        public TimeSpan DefaultHeartbeatThrottleInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the limit for the number of activities per second that this worker will
        /// process. The worker will not poll for new activities if by doing so it might receive and
        /// execute an activity which would cause it to exceed this limit.
        /// </summary>
        public double? MaxActivitiesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activities per second the task queue will dispatch,
        /// controlled server-side. Note that this only takes effect upon an activity poll request.
        /// If multiple workers on the same queue have different values set, they will thrash with
        /// the last poller winning.
        /// </summary>
        public double? MaxTaskQueueActivitiesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the amount of time after shutdown is called that activities are given to
        /// complete before they are cancelled.
        /// </summary>
        public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Add the given delegate as an activity.
        /// </summary>
        /// <param name="del">Delegate to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddActivity(Delegate del)
        {
            Activities.Add(del);
            return this;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}