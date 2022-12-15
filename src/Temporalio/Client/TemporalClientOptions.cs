namespace Temporalio.Client
{
    /// <summary>
    /// Options for a <see cref="TemporalClient" />.
    /// </summary>
    public class TemporalClientOptions
    {
        /// <summary>
        /// Gets or sets the client namespace. Default is "default".
        /// </summary>
        public string Namespace { get; set; } = "default";

        // TODO(cretz): public Converters.DataConverter DataConverter { get; set; }

        /// <summary>
        /// Create default options.
        /// </summary>
        public TemporalClientOptions()
        {
            // TODO(cretz): DataConverter = Converters.DataConverter.Default;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
