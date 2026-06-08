namespace Temporalio.Client
{
    /// <summary>
    /// Transport-level gRPC compression used for client calls.
    ///
    /// Use <see cref="Gzip"/> for gzip compression or <see cref="None"/> to opt out.
    /// </summary>
    /// <seealso cref="TemporalConnectionOptions.GrpcCompression" />
    public abstract record GrpcCompression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcCompression"/> class.
        /// </summary>
        internal GrpcCompression()
        {
        }

        /// <summary>
        /// Gzip-compress outbound requests and accept gzip-compressed responses.
        /// </summary>
        public sealed record Gzip() : GrpcCompression;

        /// <summary>
        /// Do not compress requests or advertise acceptance of compressed responses.
        /// </summary>
        public sealed record None() : GrpcCompression;
    }
}
