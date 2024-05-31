using System;

namespace Temporalio.Worker
{
    /// <summary>
    /// Represents a notification hub that can be used to push worker client updates to subscribing workers.
    /// </summary>
    public interface IWorkerClientUpdater
    {
        /// <summary>
        /// The event for when a worker client update is broadcast.
        /// </summary>
        event EventHandler<WorkerClientUpdatedEventArgs> OnWorkerClientUpdated;

        /// <summary>
        /// Dispatches a notification to all subscribers that a new worker client should be used.
        /// </summary>
        /// <param name="client">The new <see cref="IWorkerClient"/> that should be pushed out to all subscribing workers.</param>
        void UpdateWorkerClient(IWorkerClient client);
    }
}
