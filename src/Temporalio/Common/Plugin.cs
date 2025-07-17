using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Worker;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Common
{
    public class Plugin : IClientPlugin, IWorkerPlugin
    {
        private IWorkerPlugin? nextWorkerPlugin;
        private IClientPlugin? nextClientPlugin;

        public void InitClientPlugin(IClientPlugin nextPlugin) => nextClientPlugin = nextPlugin;

        public TemporalClientOptions OnCreateClient(TemporalClientOptions options) => options;

        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options) =>
            nextClientPlugin!.TemporalConnectAsync(options);

        public TemporalConnection TemporalConnect(TemporalClientConnectOptions options) =>
            nextClientPlugin!.TemporalConnect(options);

        public void InitWorkerPlugin(IWorkerPlugin nextPlugin) => nextWorkerPlugin = nextPlugin;

        public TemporalWorkerOptions OnCreateWorker(TemporalWorkerOptions options) =>
            nextWorkerPlugin!.OnCreateWorker(options);

        public Task ExecuteAsync(TemporalWorker worker, Func<Task>? untilComplete,
            CancellationToken stoppingToken = default) =>
            nextWorkerPlugin!.ExecuteAsync(worker, untilComplete, stoppingToken);
    }
}