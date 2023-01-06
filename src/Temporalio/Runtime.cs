using System;

namespace Temporalio
{
    /// <summary>
    /// Runtime for the Temporal SDK.
    /// </summary>
    /// <remarks>
    /// This runtime carries the internal core engine and telemetry options for Temporal. All
    /// connections/clients created using it, and any workers created from them, will be associated
    /// with the runtime.
    /// </remarks>
    public sealed class Runtime
    {
        /// <summary>
        /// Current version of this SDK.
        /// </summary>
        public const string Version = "0.1.0-alpha1";

        private static readonly Lazy<Runtime> _default =
            new(() => new Runtime(new Bridge.Runtime()));

        // TODO(cretz): Support for creating a runtime with telemetry options and such

        /// <summary>
        /// Create or get the default runtime.
        /// </summary>
        /// <remarks>
        /// This is lazily created when first accessed. The default runtime is accessed when a
        /// runtime is not explicitly provided to a connection/client.
        /// </remarks>
        public static Runtime Default => _default.Value;

        internal readonly Bridge.Runtime runtime;

        private Runtime(Bridge.Runtime runtime)
        {
            this.runtime = runtime;
        }
    }
}
