using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Disposable collection of items we need to keep alive while this object is in scope. NOT threadsafe.
    /// </summary>
    internal sealed class Scope : IDisposable
    {
        private readonly List<ByteArrayRef> byteArrayRefs = new();
        private readonly List<GCHandle> gcHandles = new();
        private readonly List<IDisposable> disposables = new();
        private bool disposed;

        /// <summary>
        /// Finalizes an instance of the <see cref="Scope"/> class.
        /// </summary>
        ~Scope() => Dispose(false);

        /// <summary>
        /// Create a byte array ref.
        /// </summary>
        /// <param name="bytes">Bytes to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef ByteArray(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = new ByteArrayRef(bytes);
            byteArrayRefs.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a UTF-8 byte array ref.
        /// </summary>
        /// <param name="str">String to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef ByteArray(string? str)
        {
            if (str == null || str.Length == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromUTF8(str);
            byteArrayRefs.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a metadata byte array ref.
        /// </summary>
        /// <param name="metadata">Metadata to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef Metadata(IEnumerable<KeyValuePair<string, string>>? metadata)
        {
            if (metadata == null)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromMetadata(metadata);
            byteArrayRefs.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a newline-delimited byte array ref.
        /// </summary>
        /// <param name="values">Values to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef NewlineDelimited(IEnumerable<string>? values)
        {
            if (values == null)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromNewlineDelimited(values);
            byteArrayRefs.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create an array of byte arrays from an collection of strings.
        /// </summary>
        /// <param name="strings">Strings.</param>
        /// <returns>Created byte array array.</returns>
        public Interop.TemporalCoreByteArrayRefArray ByteArrayArray(IEnumerable<string> strings)
        {
            var arr = strings.Select(ByteArray).ToArray();
            unsafe
            {
                return new()
                {
                    data = ArrayPointer(arr),
                    size = (UIntPtr)arr.Length,
                };
            }
        }

        /// <summary>
        /// Create a cancellation token.
        /// </summary>
        /// <param name="token">Cancellation token to create from.</param>
        /// <returns>Created cancellation token.</returns>
        public unsafe Interop.TemporalCoreCancellationToken* CancellationToken(
            System.Threading.CancellationToken? token)
        {
            if (token == null)
            {
                return null;
            }
            var val = new CancellationToken(token.Value);
            disposables.Add(val);
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
            gcHandles.Add(handle);
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
            gcHandles.Add(handle);
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
            gcHandles.Add(handle);
            return Marshal.GetFunctionPointerForDelegate(handle.Target!);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            byteArrayRefs.Clear();

            foreach (var handle in gcHandles)
            {
                handle.Free();
            }
            gcHandles.Clear();

            if (disposing)
            {
                foreach (var disposable in disposables)
                {
                    disposable.Dispose();
                }
            }
            disposables.Clear();

            disposed = true;
        }
    }
}
