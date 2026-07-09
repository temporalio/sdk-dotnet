using System.Threading;
using System.Threading.Tasks;
using Temporalio.Worker;

namespace Temporalio.Extensions.Aws.Lambda
{
    /// <summary>
    /// Adapter from <see cref="TemporalWorker" /> to <see cref="ILambdaWorker" />.
    /// </summary>
    internal sealed class TemporalWorkerAdapter : ILambdaWorker
    {
        private readonly TemporalWorker worker;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerAdapter"/> class.
        /// </summary>
        /// <param name="worker">Worker to adapt.</param>
        public TemporalWorkerAdapter(TemporalWorker worker) => this.worker = worker;

        /// <inheritdoc />
        public Task ExecuteAsync(CancellationToken stoppingToken) =>
            worker.ExecuteAsync(stoppingToken);

        /// <inheritdoc />
        public void Dispose() => worker.Dispose();
    }
}
