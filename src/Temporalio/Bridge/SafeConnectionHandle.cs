using Temporalio.Bridge.Interop;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Safe handle for a Temporal connection.
    /// </summary>
    internal sealed class SafeConnectionHandle :
        SafeUnmanagedHandle<TemporalCoreConnection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeConnectionHandle" /> class.
        /// </summary>
        /// <param name="ptr">Connection pointer.</param>
        public unsafe SafeConnectionHandle(TemporalCoreConnection* ptr)
            : base(ptr)
        {
        }

        /// <summary>
        /// Free the connection.
        /// </summary>
        /// <returns>Always returns <c>true</c>.</returns>
        protected override unsafe bool ReleaseHandle()
        {
            Methods.temporal_core_client_free(UnsafePtr);
            return true;
        }
    }
}
