using System;
using Temporalio.Client;

namespace Temporalio.Worker
{
    /// <summary>
    /// Event raised when a worker client is updated.
    /// </summary>
    public class WorkerClientUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerClientUpdatedEventArgs"/> class.
        /// </summary>
        /// <param name="workerClient">The client to update workers with.</param>
        public WorkerClientUpdatedEventArgs(IWorkerClient workerClient) => WorkerClient = workerClient;

        /// <summary>
        /// Gets the <see cref="ITemporalClient"/> that will be propagated to all event listeners.
        /// </summary>
        public IWorkerClient WorkerClient { get; }
    }
}
