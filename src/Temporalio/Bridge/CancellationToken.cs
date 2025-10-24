using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned cancellation token that's bound to the given .NET cancellation token.
    /// </summary>
    internal class CancellationToken : SafeHandle
    {
        private readonly System.Threading.CancellationTokenRegistration registration;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationToken"/> class.
        /// </summary>
        /// <param name="token">.NET cancellation token to bind.</param>
        public CancellationToken(System.Threading.CancellationToken token)
            : base(IntPtr.Zero, true)
        {
            unsafe
            {
                Ptr = Interop.Methods.temporal_core_cancellation_token_new();
                SetHandle((IntPtr)Ptr);
            }
            registration = token.Register(Cancel);
        }

        /// <inheritdoc/>
        public override unsafe bool IsInvalid => Ptr == null;

        /// <summary>
        /// Gets internal token pointer.
        /// </summary>
        internal unsafe Interop.TemporalCoreCancellationToken* Ptr { get; }

        /// <summary>
        /// Cancels the Core-owned token. Does not cancel the bound .NET token.
        /// </summary>
        /// <remarks>
        /// Cancelling the bound .NET token automatically cancels the Core-owned token,
        /// but not the other way around.
        /// </remarks>
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
            registration.Dispose();
            Interop.Methods.temporal_core_cancellation_token_free(Ptr);
            return true;
        }
    }
}
