namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="TemporalClient.ConnectAsync" />.
    /// <see cref="TemporalConnectionOptions.TargetHost" /> is required.
    /// </summary>
    /// <remarks>
    /// This is essentially a combination of <see cref="TemporalConnectionOptions" /> and
    /// <see cref="TemporalClientOptions" />.
    /// </remarks>
    public class TemporalClientConnectOptions : TemporalConnectionOptions
    {
        /// <summary>
        /// Create default options.
        /// </summary>
        public TemporalClientConnectOptions()
        {
            // TODO(cretz): DataConverter = Converters.DataConverter.Default;
        }

        /// <summary>
        /// Create default options with a target host.
        /// </summary>
        /// <param name="targetHost">Target host to connect to.</param>
        /// <seealso cref="TemporalConnectionOptions.TargetHost" />
        public TemporalClientConnectOptions(string targetHost) : base(targetHost)
        {
            // TODO(cretz): DataConverter = Converters.DataConverter.Default;
        }

        /// <summary>
        /// Gets or sets the client namespace. Default is "default".
        /// </summary>
        public string Namespace { get; set; } = "default";

        // TODO(cretz): public Converters.DataConverter DataConverter { get; set; }

        /// <summary>
        /// Create client options from a subset of these options for use in
        /// <see cref="TemporalClient.TemporalClient" />.
        /// </summary>
        /// <returns>Client options.</returns>
        public TemporalClientOptions ToClientOptions()
        {
            return new TemporalClientOptions()
            {
                Namespace = Namespace,
                // TODO(cretz): DataConverter = DataConverter
            };
        }
    }
}
