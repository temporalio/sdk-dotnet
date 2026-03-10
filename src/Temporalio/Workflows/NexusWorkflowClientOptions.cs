using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for creating a Nexus client.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusWorkflowClientOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusWorkflowClientOptions"/> class.
        /// </summary>
        public NexusWorkflowClientOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusWorkflowClientOptions"/> class.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        public NexusWorkflowClientOptions(string endpoint) => Endpoint = endpoint;

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