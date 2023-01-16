using System;
using System.Collections.Generic;

namespace Temporalio.Runtime
{
    /// <summary>
    /// OpenTelemetry tracing/metric collector options. <see cref="OpenTelemetryOptions.Url" /> is
    /// required.
    /// </summary>
    public class OpenTelemetryOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryOptions"/> class.
        /// </summary>
        public OpenTelemetryOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryOptions"/> class.
        /// </summary>
        /// <param name="url"><see cref="Url" />.</param>
        public OpenTelemetryOptions(string url)
        {
            Url = url;
        }

        /// <summary>
        /// Gets or sets the URL for the OpenTelemetry collector.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the headers to include in OpenTelemetry calls.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>>? Headers { get; set; }

        /// <summary>
        /// Gets or sets how frequently in metrics should be exported.
        /// </summary>
        public TimeSpan? MetricsExportInterval { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
