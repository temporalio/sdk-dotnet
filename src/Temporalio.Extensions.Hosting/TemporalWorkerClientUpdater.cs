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

        private event EventHandler<IWorkerClient>? OnClientUpdatedEvent;

        /// <summary>
        /// Dispatches a notification to all subscribers that a new worker client should be used.
        /// </summary>
        /// <param name="client">The new <see cref="IWorkerClient"/> that should be pushed out to all subscribing workers.</param>
        public void UpdateClient(IWorkerClient client)
        {
            OnClientUpdatedEvent?.Invoke(this, client);
        }

        /// <summary>
        /// Adds a new subscriber that will be notified when a new worker client should be used.
        /// </summary>
        /// <param name="eventHandler">The event handler to add to the event listeners.</param>
        internal void Subscribe(EventHandler<IWorkerClient> eventHandler)
        {
            lock (clientLock)
            {
                OnClientUpdatedEvent += eventHandler;
            }
        }

        /// <summary>
        /// Removes an existing subscriber from receiving notifications when a new worker client should be used.
        /// </summary>
        /// <param name="eventHandler">The event handler to remove from the event listeners.</param>
        internal void Unsubscribe(EventHandler<IWorkerClient> eventHandler)
        {
            lock (clientLock)
            {
                OnClientUpdatedEvent -= eventHandler;
            }
        }
    }
}
