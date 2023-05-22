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
        /// is thread safe.
        /// </summary>
        IReadOnlyCollection<KeyValuePair<string, string>> RpcMetadata { get; set; }

        /// <summary>
        /// Gets the raw workflow service.
        /// </summary>
        public WorkflowService WorkflowService { get; }

        /// <summary>
        /// Gets the raw operator service.
        /// </summary>
        public OperatorService OperatorService { get; }

        /// <summary>
        /// Gets the raw gRPC test service.
        /// </summary>
        /// <remarks>
        /// Only the <see cref="Testing.WorkflowEnvironment.StartTimeSkippingAsync" />
        /// environment has this service implemented.
        /// </remarks>
        public TestService TestService { get; }

        /// <summary>
        /// Gets the options used to create this connection.
        /// </summary>
        public TemporalConnectionOptions Options { get; }

        /// <summary>
        /// Check health for the given service type.
        /// </summary>
        /// <param name="service">Service type to check health for. Defaults to
        /// <see cref="TemporalConnection.WorkflowService" />.</param>
        /// <param name="options">RPC options for the check call.</param>
        /// <returns>True if healthy, false otherwise.</returns>
        public Task<bool> CheckHealthAsync(RpcService? service = null, RpcOptions? options = null);
    }
}
