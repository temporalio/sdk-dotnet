using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Disposable collection of items we need to keep alive while this object is in scope.
    /// </summary>
    internal sealed class Scope : IDisposable
    {
        private readonly List<object> toKeepAlive = new List<object>();

        /// <summary>
        /// Create a byte array ref.
        /// </summary>
        /// <param name="bytes">Bytes to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.ByteArrayRef ByteArray(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = new ByteArrayRef(bytes);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a UTF-8 byte array ref.
        /// </summary>
        /// <param name="str">String to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.ByteArrayRef ByteArray(string? str)
        {
            if (str == null || str.Length == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromUTF8(str);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a metadata byte array ref.
        /// </summary>
        /// <param name="metadata">Metadata to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.ByteArrayRef Metadata(IEnumerable<KeyValuePair<string, string>>? metadata)
        {
            if (metadata == null)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromMetadata(metadata);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a newline-delimited byte array ref.
        /// </summary>
        /// <param name="values">Values to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.ByteArrayRef NewlineDelimited(IEnumerable<string>? values)
        {
            if (values == null)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromNewlineDelimited(values);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a cancellation token.
        /// </summary>
        /// <param name="token">Cancellation token to create from.</param>
        /// <returns>Created cancellation token.</returns>
        public unsafe Interop.CancellationToken* CancellationToken(
            System.Threading.CancellationToken? token)
        {
            if (token == null)
            {
                return null;
            }
            var val = Temporalio.Bridge.CancellationToken.FromThreading(token.Value);
            toKeepAlive.Add(val);
            return val.Ptr;
        }

        /// <summary>
        /// Create a stable pointer to an object.
        /// </summary>
        /// <typeparam name="T">Type of the object.</typeparam>
        /// <param name="value">Object to get create pointer for.</param>
        /// <returns>Created pointer.</returns>
        public unsafe T* Pointer<T>(T value)
            where T : unmanaged
        {
            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            toKeepAlive.Add(handle);
            return (T*)handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Create a stable pointer to an object.
        /// </summary>
        /// <typeparam name="T">Type of the object.</typeparam>
        /// <param name="value">Object to get create pointer for.</param>
        /// <returns>Created pointer.</returns>
        public unsafe T* ArrayPointer<T>(T[] value)
            where T : unmanaged
        {
            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            toKeepAlive.Add(handle);
            return (T*)handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Create function pointer for delegate.
        /// </summary>
        /// <typeparam name="T">Delegate type.</typeparam>
        /// <param name="func">Delegate to create pointer for.</param>
        /// <returns>Created pointer.</returns>
        public IntPtr FunctionPointer<T>(T func)
            where T : Delegate
        {
            // The delegate seems to get collected before called sometimes even if we add "func" to
            // the keep alive list. Delegates are supposed to be reference types, but their pointers
            // seem unstable. So we're going to alloc a handle for it. We can't pin it though.
            var handle = GCHandle.Alloc(func);
            toKeepAlive.Add(handle);
            return Marshal.GetFunctionPointerForDelegate(handle.Target!);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var v in toKeepAlive)
            {
                if (v is GCHandle handle)
                {
                    handle.Free();
                }
            }
            // This keep alive does nothing obviously, but it's good documentation to understand the
            // purpose of this separate dispose call
            GC.KeepAlive(toKeepAlive);
            GC.SuppressFinalize(this);
        }
    }
}
