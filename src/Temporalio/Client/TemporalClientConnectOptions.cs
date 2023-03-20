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
        /// Initializes a new instance of the <see cref="TemporalClientConnectOptions"/> class with
        /// default options.
        /// </summary>
        public TemporalClientConnectOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalClientConnectOptions"/> class with
        /// default options and a target host.
        /// </summary>
        /// <param name="targetHost">Target host to connect to.</param>
        /// <seealso cref="TemporalConnectionOptions.TargetHost" />
        public TemporalClientConnectOptions(string targetHost)
            : base(targetHost)
        {
        }

        /// <summary>
        /// Gets or sets the client namespace. Default is "default".
        /// </summary>
        public string Namespace { get; set; } = "default";

        /// <summary>
        /// Gets or sets the client data converter. Default is
        /// <see cref="Converters.DataConverter.Default" />.
        /// </summary>
        public Converters.DataConverter DataConverter { get; set; } =
            Converters.DataConverter.Default;

        /// <summary>
        /// Create client options from a subset of these options for use in
        /// <see cref="TemporalClient.TemporalClient" />.
        /// </summary>
        /// <returns>Client options.</returns>
        public TemporalClientOptions ToClientOptions() =>
            new()
            {
                Namespace = Namespace,
                DataConverter = DataConverter,
            };
    }
}
