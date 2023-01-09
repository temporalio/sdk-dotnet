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
        /// URL for the OpenTelemetry collector.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Headers to include in OpenTelemetry calls.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>>? Headers { get; set; }

        /// <summary>
        /// How frequently in metrics should be exported.
        /// </summary>
        public TimeSpan? MetricPeriodicity { get; set; }

        /// <summary>
        /// Create unset OpenTelemetry options.
        /// </summary>
        public OpenTelemetryOptions() { }

        /// <summary>
        /// Create OpenTelemetry options with the given URL.
        /// </summary>
        /// <param name="url"><see cref="OpenTelemetryOptions.Url" /></param>
        public OpenTelemetryOptions(string url)
        {
            Url = url;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
