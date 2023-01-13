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
    public class TemporaliteOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the existing executable path for Temporalite.
        /// </summary>
        public string? ExistingPath { get; set; }

        /// <summary>
        /// Gets or sets the database filename for Temporalite.
        /// </summary>
        /// <remarks>
        /// By default, an in-memory database is used.
        /// </remarks>
        public string? DatabaseFilename { get; set; }

        /// <summary>
        /// Gets or sets the log format for Temporalite.
        /// </summary>
        public string LogFormat { get; set; } = "pretty";

        /// <summary>
        /// Gets or sets the log level for Temporalite.
        /// </summary>
        public string LogLevel { get; set; } = "warn";

        /// <summary>
        /// Gets or sets the version to version of Temporalite to download.
        /// </summary>
        /// <remarks>
        /// By default, the best one for this SDK version is chosen. This can be a semantic version,
        /// "latest", or "default".
        /// </remarks>
        public string DownloadVersion { get; set; } = "default";

        /// <summary>
        /// Gets or sets the extra arguments for Temporalite.
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
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
