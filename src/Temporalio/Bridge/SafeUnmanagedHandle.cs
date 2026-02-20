using System;
using Microsoft.Win32.SafeHandles;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Safe handle for unmanaged instance.
    /// </summary>
    /// <typeparam name="T">Unmanaged type.</typeparam>
    internal abstract class SafeUnmanagedHandle<T> :
        SafeHandleZeroOrMinusOneIsInvalid
        where T : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeUnmanagedHandle{T}" /> class.
        /// </summary>
        /// <param name="ptr">Unmanaged pointer.</param>
        public unsafe SafeUnmanagedHandle(T* ptr)
            : base(true)
        {
            SetHandle((IntPtr)ptr);
        }

        /// <summary>
        /// Gets the unsafe pointer of the unmanaged instance.
        /// </summary>
        protected unsafe T* UnsafePtr => (T*)DangerousGetHandle();
    }
}
