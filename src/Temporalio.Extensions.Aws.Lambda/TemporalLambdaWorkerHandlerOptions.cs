using System;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;

namespace Temporalio.Extensions.Aws.Lambda
{
    /// <summary>
    /// Internal test seams for the Lambda worker handler.
    /// </summary>
    internal sealed class TemporalLambdaWorkerHandlerOptions
    {
        /// <summary>
        /// Gets or sets the environment variable reader.
        /// </summary>
        public Func<string, string?> GetEnvironmentVariable { get; set; } =
            Environment.GetEnvironmentVariable;

        /// <summary>
        /// Gets or sets the client configuration loader.
        /// </summary>
        public Func<ClientEnvConfig.ProfileLoadOptions?, TemporalClientConnectOptions>? LoadClientConnectOptions { get; set; }

        /// <summary>
        /// Gets or sets the client connection factory.
        /// </summary>
        public Func<TemporalClientConnectOptions, Task<object>> ConnectClientAsync { get; set; } =
            async options => await TemporalClient.ConnectAsync(options).ConfigureAwait(false);

        /// <summary>
        /// Gets or sets the worker factory.
        /// </summary>
        public Func<object, TemporalWorkerOptions, ILambdaWorker> CreateWorker { get; set; } =
            (client, options) => new TemporalWorkerAdapter(
                new TemporalWorker((IWorkerClient)client, options));
    }
}
