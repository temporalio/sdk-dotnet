using System;
using Temporalio.Common;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Runtime for the Temporal SDK.
    /// </summary>
    /// <remarks>
    /// This runtime carries the internal core engine and telemetry options for Temporal. All
    /// connections/clients created using it, and any workers created from them, will be associated
    /// with the runtime.
    /// </remarks>
    public sealed class TemporalRuntime
    {
        /// <summary>
        /// Prefix for reserved handler and definition names.
        /// </summary>
        internal const string ReservedNamePrefix = "__temporal";

        private static readonly Lazy<TemporalRuntime> LazyDefault =
            new(() => new TemporalRuntime(new TemporalRuntimeOptions()));

        private readonly Lazy<MetricMeter> metricMeter;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalRuntime"/> class.
        /// </summary>
        /// <param name="options">Options for the new runtime.</param>
        /// <remarks>
        /// This creates an entirely new thread pool and runtime in the Core backend. Please use
        /// sparingly.
        /// </remarks>
        public TemporalRuntime(TemporalRuntimeOptions options)
            : this(new Bridge.Runtime(options))
        {
        }

        private TemporalRuntime(Bridge.Runtime runtime)
        {
            Runtime = runtime;
            metricMeter = MetricMeterBridge.LazyFromRuntime(runtime);
        }

        /// <summary>
        /// Gets or creates the default runtime.
        /// </summary>
        /// <remarks>
        /// This is lazily created when first accessed. The default runtime is accessed when a
        /// runtime is not explicitly provided to a connection/client.
        /// </remarks>
        public static TemporalRuntime Default => LazyDefault.Value;

        /// <summary>
        /// Gets the metric meter associated with this runtime.
        /// </summary>
        public MetricMeter MetricMeter => metricMeter.Value;

        /// <summary>
        /// Gets the runtime.
        /// </summary>
        internal Bridge.Runtime Runtime { get; private init; }
    }
}
