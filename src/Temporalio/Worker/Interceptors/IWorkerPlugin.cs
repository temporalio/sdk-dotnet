using System;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CA1822 // We don't want to force plugin methods to be static

namespace Temporalio.Worker.Interceptors
{
    public interface IWorkerPlugin
    {
        public void InitWorkerPlugin(IWorkerPlugin nextPlugin);

        public TemporalWorkerOptions OnCreateWorker(TemporalWorkerOptions options);

        public Task ExecuteAsync(
            TemporalWorker worker, Func<Task>? untilComplete, CancellationToken stoppingToken = default);
    }
}