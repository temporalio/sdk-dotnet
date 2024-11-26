using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Extend this class to help with making a class that has callbacks which are invoked by Rust.
    /// </summary>
    /// <typeparam name="T">The native type that holds the function ptrs for callbacks to C#.</typeparam>
    internal class NativeInvokeableClass<T>
    where T : unmanaged
    {
        private readonly List<GCHandle> handles = new();

        /// <summary>
        /// Gets the pointer to the native callback holder.
        /// </summary>
        internal unsafe T* Ptr { get; private set; }

        /// <summary>
        /// Pin the native type in memory and add it to the handle list. Call this after adding
        /// the callbacks via <see cref="FunctionPointer"/>. Also adds `this` to the handle list.
        /// </summary>
        /// <param name="value">The native type to pin.</param>
        internal void PinCallbackHolder(T value)
        {
            // Pin the callback holder & set it as the first handle
            var holderHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
            handles.Insert(0, holderHandle);
            unsafe
            {
                Ptr = (T*)holderHandle.AddrOfPinnedObject();
            }
            // Add handle for ourself
            handles.Add(GCHandle.Alloc(this));
        }

        /// <summary>
        /// Make a handle for and return a C# method as a callback for Rust to invoke.
        /// </summary>
        /// <typeparam name="TF">The native type of the function pointer.</typeparam>
        /// <param name="func">The C# method to use for the callback.</param>
        /// <returns>The function pointer to the C# method.</returns>
        internal IntPtr FunctionPointer<TF>(TF func)
            where TF : Delegate
        {
            var handle = GCHandle.Alloc(func);
            handles.Add(handle);
            return Marshal.GetFunctionPointerForDelegate(handle.Target!);
        }

        /// <summary>
        /// Free the memory of the native type and all the function pointers.
        /// </summary>
        /// <param name="meter">The native type to free.</param>
        internal unsafe void Free(T* meter)
        {
            // Free in order which frees function pointers first then object handles
            foreach (var handle in handles)
            {
                handle.Free();
            }
        }
    }
}
