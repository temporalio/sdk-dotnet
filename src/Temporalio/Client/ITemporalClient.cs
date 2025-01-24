namespace Temporalio.Client
{
    /// <summary>
    /// Interface to a client to Temporal.
    /// </summary>
    /// <remarks>
    /// Clients are thread-safe and are encouraged to be reused to properly reuse the underlying
    /// connection.
    /// </remarks>
    /// <seealso cref="TemporalClient" />
    public partial interface ITemporalClient : Worker.IWorkerClient
    {
        /// <summary>
        /// Gets the connection associated with this client.
        /// </summary>
        ITemporalConnection Connection { get; }

        /// <summary>
        /// Gets the outbound interceptor in use.
        /// </summary>
        Interceptors.ClientOutboundInterceptor OutboundInterceptor { get; }

        /// <summary>
        /// Gets the raw gRPC workflow service. Most users do not need this.
        /// </summary>
        WorkflowService WorkflowService { get; }

        /// <summary>
        /// Gets the raw gRPC operator service for self-hosted servers. Most users do not need this.
        /// </summary>
        OperatorService OperatorService { get; }
    }
}
