using System;

namespace Temporalio.Worker
{
    /// <summary>
    /// Implementation of the <see cref="IWorkerClientUpdater"/>.
    /// </summary>
    public class WorkerClientUpdater : IWorkerClientUpdater
    {
        private readonly object clientLock = new();

        /// <summary>
        /// The <see cref="EventHandler"/> used to dispatch notifications to subscribers that the client was updated.
        /// </summary>
        public event EventHandler<WorkerClientUpdatedEventArgs> OnWorkerClientUpdated
        {
            add
            {
                lock (clientLock)
                {
                    WorkerClientUpdatedEvent += value;
                }
            }

            remove
            {
                lock (clientLock)
                {
                    WorkerClientUpdatedEvent -= value;
                }
            }
        }

        private event EventHandler<WorkerClientUpdatedEventArgs>? WorkerClientUpdatedEvent;

        /// <inheritdoc />
        public void UpdateWorkerClient(IWorkerClient client)
        {
            WorkerClientUpdatedEventArgs eventArgs = new WorkerClientUpdatedEventArgs(client);

            WorkerClientUpdatedEvent?.Invoke(this, eventArgs);
        }
    }
}
