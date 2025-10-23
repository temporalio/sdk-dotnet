using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned cancellation token.
    /// </summary>
    internal class CancellationToken : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationToken"/> class.
        /// </summary>
        public CancellationToken()
            : base(IntPtr.Zero, true)
        {
            unsafe
            {
                Ptr = Interop.Methods.temporal_core_cancellation_token_new();
                SetHandle((IntPtr)Ptr);
            }
        }

        /// <inheritdoc/>
        public override unsafe bool IsInvalid => Ptr == null;

        /// <summary>
        /// Gets internal token pointer.
        /// </summary>
        internal unsafe Interop.TemporalCoreCancellationToken* Ptr { get; }

        /// <summary>
        /// Create a core cancellation token from the given cancellation token.
        /// </summary>
        /// <param name="token">Threading token.</param>
        /// <returns>Created cancellation token.</returns>
        public static CancellationToken FromThreading(System.Threading.CancellationToken token)
        {
            var ret = new CancellationToken();
            token.Register(ret.Cancel);
            return ret;
        }

        /// <summary>
        /// Cancel this token.
        /// </summary>
        public void Cancel()
        {
            if (!IsClosed)
            {
                unsafe
                {
                    Interop.Methods.temporal_core_cancellation_token_cancel(Ptr);
                }
            }
        }

        /// <inheritdoc/>
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.temporal_core_cancellation_token_free(Ptr);
            return true;
        }
    }
}
