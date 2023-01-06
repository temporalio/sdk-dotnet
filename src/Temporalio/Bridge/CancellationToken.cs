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
        /// Create a core cancellation token from the given cancellation token.
        /// </summary>
        public static CancellationToken FromThreading(System.Threading.CancellationToken token)
        {
            var ret = new CancellationToken();
            token.Register(ret.Cancel);
            return ret;
        }

        internal readonly unsafe Interop.CancellationToken* ptr;

        public CancellationToken() : base(IntPtr.Zero, true)
        {
            unsafe
            {
                ptr = Interop.Methods.cancellation_token_new();
                SetHandle((IntPtr)ptr);
            }
        }

        public override unsafe bool IsInvalid => false;

        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.cancellation_token_free(ptr);
            return true;
        }

        public void Cancel()
        {
            unsafe
            {
                Interop.Methods.cancellation_token_cancel(ptr);
            }
        }
    }
}
