using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Interface to a connection to Temporal.
    /// </summary>
    /// <seealso cref="TemporalConnection" />
    public interface ITemporalConnection : IBridgeClientProvider
    {
        /// <summary>
        /// Gets or sets the current RPC metadata (i.e. the headers). This can be updated which will
        /// apply to all future calls the client makes including inside a worker. Setting this value
        /// is thread safe. When setting, this will error if the client is not already connected
        /// (e.g. a lazy client has not made a call).
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Client is not already
        /// connected.</exception>
        IReadOnlyCollection<KeyValuePair<string, string>> RpcMetadata { get; set; }

        /// <summary>
        /// Gets the raw workflow service.
        /// </summary>
        WorkflowService WorkflowService { get; }

        /// <summary>
        /// Gets the raw operator service.
        /// </summary>
        OperatorService OperatorService { get; }

        /// <summary>
        /// Gets the raw gRPC test service.
        /// </summary>
        /// <remarks>
        /// Only the <see cref="Testing.WorkflowEnvironment.StartTimeSkippingAsync" />
        /// environment has this service implemented.
        /// </remarks>
        TestService TestService { get; }

        /// <summary>
        /// Gets the options used to create this connection.
        /// </summary>
        TemporalConnectionOptions Options { get; }

        /// <summary>
        /// Gets a value indicating whether the client is connected. This is always true unless the
        /// client is lazy.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Check health for the given service type.
        /// </summary>
        /// <param name="service">Service type to check health for. Defaults to
        /// <see cref="TemporalConnection.WorkflowService" />.</param>
        /// <param name="options">RPC options for the check call.</param>
        /// <returns>True if healthy, false otherwise.</returns>
        Task<bool> CheckHealthAsync(RpcService? service = null, RpcOptions? options = null);

        /// <summary>
        /// Attempts connect if not already connected. Does nothing if already connected.
        /// </summary>
        /// <returns>Task for successful connection.</returns>
        Task ConnectAsync();
    }
}
