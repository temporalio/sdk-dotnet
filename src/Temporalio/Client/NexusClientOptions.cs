using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for creating a Nexus client.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusClientOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusClientOptions"/> class.
        /// </summary>
        public NexusClientOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusClientOptions"/> class.
        /// </summary>
        /// <param name="endpoint">Endpoint name.</param>
        public NexusClientOptions(string endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        /// Gets or sets the endpoint name, resolved to a URL via the cluster's endpoint registry.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
