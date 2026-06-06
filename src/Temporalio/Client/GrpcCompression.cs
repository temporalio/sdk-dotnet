namespace Temporalio.Client
{
    /// <summary>
    /// Transport-level gRPC compression used for client calls.
    /// </summary>
    /// <seealso cref="TemporalConnectionOptions.GrpcCompression" />
    public enum GrpcCompression
    {
        /// <summary>
        /// Do not compress requests or advertise acceptance of compressed responses.
        /// </summary>
        None,

        /// <summary>
        /// Gzip-compress outbound requests and accept gzip-compressed responses.
        /// </summary>
        Gzip,
    }
}
