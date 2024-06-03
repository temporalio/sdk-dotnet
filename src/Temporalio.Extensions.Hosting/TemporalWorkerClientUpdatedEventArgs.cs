using System;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Event raised when a worker client is updated.
    /// </summary>
    public class TemporalWorkerClientUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerClientUpdatedEventArgs"/> class.
        /// </summary>
        /// <param name="workerClient">The client to update workers with.</param>
        public TemporalWorkerClientUpdatedEventArgs(IWorkerClient workerClient) => WorkerClient = workerClient;

        /// <summary>
        /// Gets the <see cref="IWorkerClient"/> that will be propagated to all event listeners.
        /// </summary>
        public IWorkerClient WorkerClient { get; }
    }
}
