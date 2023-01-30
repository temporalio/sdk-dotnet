namespace Temporalio.Client
{
    /// <summary>
    /// Interface to a client to Temporal.
    /// </summary>
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
    }
}
