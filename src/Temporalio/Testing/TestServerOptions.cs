using System;
using System.Collections.Generic;

namespace Temporalio.Testing
{
    /// <summary>
    /// <b>Unstable</b> options for a time-skipping workflow environment.
    /// </summary>
    /// <remarks>
    /// <b>WARNING: This API is subject to change/removal</b>
    /// </remarks>
    public class TestServerOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the existing executable path for the test server.
        /// </summary>
        public string? ExistingPath { get; set; }

        /// <summary>
        /// Gets or sets the version to version of the test server to download. Default is
        /// "default".
        /// </summary>
        /// <remarks>
        /// By default, the best one for this SDK version is chosen. This can be a semantic version,
        /// "latest", or "default".
        /// </remarks>
        public string DownloadVersion { get; set; } = "default";

        /// <summary>
        /// Gets or sets the extra arguments for the test server.
        /// </summary>
        /// <remarks>
        /// Newlines are not allowed in values.
        /// </remarks>
        public IReadOnlyCollection<string>? ExtraArgs { get; set; }

        /// <summary>
        /// Gets or sets how long the automatic download should be cached for. If null, cached
        /// indefinitely.
        /// </summary>
        public TimeSpan? DownloadTtl { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        /// <remarks>Does not create a copy of the extra args.</remarks>
        public virtual object Clone() => MemberwiseClone();
    }
}
