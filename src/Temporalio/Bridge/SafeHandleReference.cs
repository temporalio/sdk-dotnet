using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// A reference to a safe handle that can be used to manage the lifecycle of the handle.
    /// </summary>
    /// <typeparam name="T">The type of the safe handle to reference.</typeparam>
    internal sealed class SafeHandleReference<T> :
        IDisposable
        where T : SafeHandle
    {
        private readonly bool owned;

        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SafeHandleReference{T}" /> class.
        /// </summary>
        /// <param name="handle">The safe handle to add a reference to.</param>
        /// <param name="owned">Indicates whether the safe handle is owned by this instance.</param>
        private SafeHandleReference(T handle, bool owned)
        {
            Handle = handle;
            this.owned = owned;
        }

        /// <summary>
        /// Gets the safe handle that this reference owns or references.
        /// </summary>
        public T Handle { get; private set; }

        /// <summary>
        /// Creates a new <see cref="SafeHandleReference{T}" /> instance that owns the safe handle.
        /// </summary>
        /// <param name="handle">The safe handle to own.</param>
        /// <returns>A new <see cref="SafeHandleReference{T}" /> instance that owns the safe handle.</returns>
        public static SafeHandleReference<T> Owned(T handle)
        {
            return new SafeHandleReference<T>(handle, owned: true);
        }

        /// <summary>
        /// Creates a new <see cref="SafeHandleReference{T}" /> instance that references the safe handle.
        /// </summary>
        /// <param name="handle">The safe handle to reference.</param>
        /// <returns>A new <see cref="SafeHandleReference{T}" /> instance that references the safe handle.</returns>
        /// <remarks>
        /// This will increment the reference count of the safe handle.
        /// Dispose the returned instance to decrement the reference count.
        /// </remarks>
        public static SafeHandleReference<T> AddRef(T handle)
        {
            bool success = false;
            handle.DangerousAddRef(ref success);
            if (!success)
            {
                // Documentation states that DangerousAddRef will throw if the handle is disposed
                // (and there should be no other condition under which refAdded would remain false);
                // throw just in case it returns without throwing.
                throw new InvalidOperationException("Safe handle is no longer valid.");
            }

            return new SafeHandleReference<T>(handle, owned: false);
        }

        /// <summary>
        /// Releases the reference to the safe handle.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            if (owned)
            {
                Handle.Dispose();
            }
            else
            {
                Handle.DangerousRelease();
            }
        }
    }
}
