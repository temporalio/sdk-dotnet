using System;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.Aws.Lambda
{
    /// <summary>
    /// Internal abstraction for a worker that can run for a Lambda invocation.
    /// </summary>
    internal interface ILambdaWorker : IDisposable
    {
        /// <summary>
        /// Run the worker until cancellation or failure.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token for worker shutdown.</param>
        /// <returns>Task for worker completion.</returns>
        Task ExecuteAsync(CancellationToken stoppingToken);
    }
}
