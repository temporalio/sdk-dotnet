namespace Temporalio.Runtime
{
    /// <summary>
    /// Protocol for OpenTelemetry metrics.
    /// </summary>
    public enum OpenTelemetryProtocol
    {
        /// <summary>
        /// Grpc.
        /// </summary>
        Grpc,

        /// <summary>
        /// Http.
        /// </summary>
        Http,
    }
}
