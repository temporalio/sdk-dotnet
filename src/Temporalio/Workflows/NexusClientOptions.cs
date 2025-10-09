using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for creating a Nexus client.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
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
        /// <param name="endpoint">Endpoint.</param>
        public NexusClientOptions(string endpoint) => Endpoint = endpoint;

        /// <summary>
        /// Gets or sets the endpoint.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}