using Temporalio.Bridge.Interop;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Safe handle for a Temporal client.
    /// </summary>
    internal sealed class SafeClientHandle :
        SafeUnmanagedHandle<TemporalCoreClient>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeClientHandle" /> class.
        /// </summary>
        /// <param name="ptr">Worker pointer.</param>
        public unsafe SafeClientHandle(TemporalCoreClient* ptr)
            : base(ptr)
        {
        }

        /// <summary>
        /// Free the client.
        /// </summary>
        /// <returns>Always returns <c>true</c>.</returns>
        protected override unsafe bool ReleaseHandle()
        {
            Methods.temporal_core_client_free(UnsafePtr);
            return true;
        }
    }
}
