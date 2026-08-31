using System;

namespace Temporalio.Extensions.Gcp.CloudRun
{
    /// <summary>
    /// Options for <see cref="CloudRunPlugin" />.
    /// </summary>
    /// <remarks>WARNING: Google Cloud Run support is experimental.</remarks>
    public class CloudRunPluginOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets pre-fetched Cloud Run metadata for the plugin to use.
        /// </summary>
        /// <remarks>
        /// When set, the plugin uses this metadata directly and never contacts the metadata server,
        /// so <see cref="MetadataUri" /> and <see cref="Timeout" /> are ignored. This is primarily
        /// for tests and advanced scenarios that resolve the metadata themselves.
        /// </remarks>
        public GoogleCloudRunMetadata? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the metadata server URI for the Cloud Run instance id.
        /// </summary>
        /// <remarks>
        /// When null, the default Cloud Run instance-id endpoint is used. Ignored when
        /// <see cref="Metadata" /> is set.
        /// </remarks>
        public Uri? MetadataUri { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the metadata request.
        /// </summary>
        /// <remarks>
        /// When null, a default of two seconds is used. Ignored when <see cref="Metadata" /> is set.
        /// </remarks>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
