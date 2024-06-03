using System;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Subscribes to the <see cref="TemporalWorkerClientUpdater"/>, and propagates the worker client changes to the Temporal worker.
    /// </summary>
    public class TemporalWorkerClientUpdateSubscriber : IDisposable
    {
        private readonly TemporalWorkerClientUpdater temporalWorkerClientUpdater;
        private readonly TemporalWorker worker;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerClientUpdateSubscriber"/> class.
        /// </summary>
        /// <param name="temporalWorkerClientUpdater">The optional <see cref="TemporalWorkerClientUpdater"/> used to subscribe to updates.</param>
        /// <param name="worker">The <see cref="TemporalWorker"/> that will be updated when the worker client updates.</param>
        public TemporalWorkerClientUpdateSubscriber(
            TemporalWorkerClientUpdater temporalWorkerClientUpdater,
            TemporalWorker worker)
        {
            this.temporalWorkerClientUpdater = temporalWorkerClientUpdater;
            this.worker = worker;
            this.temporalWorkerClientUpdater.TemporalWorkerClientUpdated += OnTemporalWorkerClientUpdated;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="TemporalWorkerClientUpdateSubscriber"/> class.
        /// </summary>
        ~TemporalWorkerClientUpdateSubscriber() => Dispose(false);

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Unsubscribes from the worker client updater if one exists.
        /// </summary>
        /// <param name="disposing">If set to <see langword="true"/>, the worker will unsubscribe from the worker client updater.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    temporalWorkerClientUpdater.TemporalWorkerClientUpdated -= OnTemporalWorkerClientUpdated;
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Callback invoked when a worker client updated is pushed through the <see cref="TemporalWorkerClientUpdater"/>.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="eventArgs">The <see cref="TemporalWorkerClientUpdatedEventArgs"/> of the event.</param>
        private void OnTemporalWorkerClientUpdated(object? sender, TemporalWorkerClientUpdatedEventArgs eventArgs)
        {
            worker.Client = eventArgs.WorkerClient;
        }
    }
}
