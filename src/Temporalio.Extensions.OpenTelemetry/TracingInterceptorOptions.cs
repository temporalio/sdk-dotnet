using System;
using OpenTelemetry.Context.Propagation;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Options for creating an interceptor.
    /// </summary>
    public class TracingInterceptorOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the Temporal header key.
        /// </summary>
        public string HeaderKey { get; set; } = "_tracer-data";

        /// <summary>
        /// Gets or sets the propagator.
        /// </summary>
        public TextMapPropagator Propagator { get; set; } = Propagators.DefaultTextMapPropagator;

        /// <summary>
        /// Gets or sets the tag name for workflow IDs. If null, no tag is created.
        /// </summary>
        public string? TagNameWorkflowID { get; set; } = "temporalWorkflowID";

        /// <summary>
        /// Gets or sets the tag name for run IDs. If null, no tag is created.
        /// </summary>
        public string? TagNameRunID { get; set; } = "temporalRunID";

        /// <summary>
        /// Gets or sets the tag name for activity IDs. If null, no tag is created.
        /// </summary>
        public string? TagNameActivityID { get; set; } = "temporalActivityID";

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}