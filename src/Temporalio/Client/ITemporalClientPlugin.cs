using System;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Interface for temporal client plugins.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public interface ITemporalClientPlugin
    {
        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Configures the client options.
        /// </summary>
        /// <param name="options">The client options to configure.</param>
        public void ConfigureClient(TemporalClientOptions options);

        /// <summary>
        /// Handles temporal connection asynchronously.
        /// </summary>
        /// <param name="options">The connection options.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options, Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation);
    }
}