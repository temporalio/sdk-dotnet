using System;

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
        /// Current version of this SDK.
        /// </summary>
        public const string Version = "0.1.0-alpha1";

        private static readonly Lazy<TemporalRuntime> _default =
            new(() => new TemporalRuntime(new TemporalRuntimeOptions()));

        /// <summary>
        /// Create or get the default runtime.
        /// </summary>
        /// <remarks>
        /// This is lazily created when first accessed. The default runtime is accessed when a
        /// runtime is not explicitly provided to a connection/client.
        /// </remarks>
        public static TemporalRuntime Default => _default.Value;

        internal readonly Bridge.Runtime runtime;

        /// <summary>
        /// Create a new Temporal runtime with the given options.
        /// </summary>
        /// <param name="options">Options for the new runtime</param>
        /// <remarks>
        /// This creates an entirely new thread pool and runtime in the Core backend. Please use
        /// sparingly.
        /// </remarks>
        public TemporalRuntime(TemporalRuntimeOptions options) : this(new Bridge.Runtime(options))
        { }

        private TemporalRuntime(Bridge.Runtime runtime)
        {
            this.runtime = runtime;
        }
    }
}
