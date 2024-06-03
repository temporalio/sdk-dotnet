using System;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Notification hub that can be used to push Temporal worker client updates to subscribing Temporal workers.
    /// </summary>
    public class TemporalWorkerClientUpdater
    {
        private readonly object clientLock = new();

        /// <summary>
        /// The <see cref="EventHandler"/> used to dispatch notifications to subscribers that the Temporal worker client was updated.
        /// </summary>
        public event EventHandler<TemporalWorkerClientUpdatedEventArgs> TemporalWorkerClientUpdated
        {
            add
            {
                lock (clientLock)
                {
                    TemporalWorkerClientUpdatedEvent += value;
                }
            }

            remove
            {
                lock (clientLock)
                {
                    TemporalWorkerClientUpdatedEvent -= value;
                }
            }
        }

        private event EventHandler<TemporalWorkerClientUpdatedEventArgs>? TemporalWorkerClientUpdatedEvent;

        /// <summary>
        /// Dispatches a notification to all subscribers that a new worker client should be used.
        /// </summary>
        /// <param name="client">The new <see cref="IWorkerClient"/> that should be pushed out to all subscribing workers.</param>
        public void UpdateTemporalWorkerClient(IWorkerClient client)
        {
            TemporalWorkerClientUpdatedEventArgs eventArgs = new TemporalWorkerClientUpdatedEventArgs(client);

            TemporalWorkerClientUpdatedEvent?.Invoke(this, eventArgs);
        }
    }
}
