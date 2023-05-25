using System;
using System.Collections.Generic;

namespace Temporalio.Testing
{
    /// <summary>
    /// <b>Unstable</b> options for a local workflow environment.
    /// </summary>
    /// <remarks>
    /// <b>WARNING: This API is subject to change/removal</b>
    /// </remarks>
    public class DevServerOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the existing executable path for the dev server.
        /// </summary>
        public string? ExistingPath { get; set; }

        /// <summary>
        /// Gets or sets the database filename for the dev server.
        /// </summary>
        /// <remarks>
        /// By default, an in-memory database is used.
        /// </remarks>
        public string? DatabaseFilename { get; set; }

        /// <summary>
        /// Gets or sets the log format for the dev server. Default is "pretty".
        /// </summary>
        public string LogFormat { get; set; } = "pretty";

        /// <summary>
        /// Gets or sets the log level for the dev server. Default is "warn".
        /// </summary>
        public string LogLevel { get; set; } = "warn";

        /// <summary>
        /// Gets or sets the version to version of the dev server to download. Default is "default".
        /// </summary>
        /// <remarks>
        /// By default, the best one for this SDK version is chosen. This can be a semantic version,
        /// "latest", or "default".
        /// </remarks>
        public string DownloadVersion { get; set; } = "default";

        /// <summary>
        /// Gets or sets the extra arguments for the dev server.
        /// </summary>
        /// <remarks>
        /// Newlines are not allowed in values.
        /// </remarks>
        public IEnumerable<string>? ExtraArgs { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        /// <remarks>Does not create a copy of the extra args.</remarks>
        public virtual object Clone() => MemberwiseClone();
    }
}
