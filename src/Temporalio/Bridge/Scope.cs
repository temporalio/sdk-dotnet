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
        private static readonly Interop.TemporalCoreByteArrayRefArray EmptyByteArrayRefArray =
            new()
            {
                data = null,
                size = UIntPtr.Zero,
            };

        private readonly List<GCHandle> gcHandles = new();
        private readonly List<IDisposable> disposables = new();
        private bool disposed;

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
            disposables.Add(val);
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
            disposables.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a key-value pair byte array ref.
        /// </summary>
        /// <param name="pair">Key-value pair to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef ByteArray(KeyValuePair<string, string> pair)
        {
            var val = ByteArrayRef.FromKeyValuePair(pair);
            disposables.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a key-value pair byte array ref.
        /// </summary>
        /// <param name="pair">Key-value pair to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef ByteArray(KeyValuePair<string, byte[]> pair)
        {
            var val = ByteArrayRef.FromKeyValuePair(pair);
            disposables.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a newline-delimited byte array ref.
        /// </summary>
        /// <param name="values">Values to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef NewlineDelimited(IReadOnlyCollection<string>? values)
        {
            if (values == null || values.Count == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromNewlineDelimited(values);
            disposables.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create a newline-delimited byte array ref.
        /// </summary>
        /// <param name="values">Values to create from.</param>
        /// <returns>Created byte array ref.</returns>
        public Interop.TemporalCoreByteArrayRef NewlineDelimited(IReadOnlyCollection<KeyValuePair<string, string>>? values)
        {
            if (values == null || values.Count == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromNewlineDelimited(values);
            disposables.Add(val);
            return val.Ref;
        }

        /// <summary>
        /// Create an array of byte arrays from an collection of strings.
        /// </summary>
        /// <param name="strings">Strings.</param>
        /// <returns>Created byte array array.</returns>
        public Interop.TemporalCoreByteArrayRefArray ByteArrayArray(IReadOnlyCollection<string> strings)
        {
            if (strings == null || strings.Count == 0)
            {
                return EmptyByteArrayRefArray;
            }

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
        /// Create an array of byte arrays from an collection of key-value pairs.
        /// </summary>
        /// <param name="values">Values.</param>
        /// <returns>Created byte array array.</returns>
        public Interop.TemporalCoreByteArrayRefArray ByteArrayArray(IReadOnlyCollection<KeyValuePair<string, string>>? values)
        {
            if (values == null || values.Count == 0)
            {
                return EmptyByteArrayRefArray;
            }

            var arr = values.Select(ByteArray).ToArray();
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
        /// Create an array of byte arrays from an collection of key-value pairs.
        /// </summary>
        /// <param name="values">Values.</param>
        /// <returns>Created byte array array.</returns>
        public Interop.TemporalCoreByteArrayRefArray ByteArrayArray(IReadOnlyCollection<KeyValuePair<string, byte[]>>? values)
        {
            if (values == null || values.Count == 0)
            {
                return EmptyByteArrayRefArray;
            }

            var arr = values.Select(ByteArray).ToArray();
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
            // Note, we intentionally do not free or call Dispose from a finalizer. When working
            // with native objects, best practice is to not free in finalizer. On process shutdown,
            // finalizers can be called even when the object is still referenced.
            if (disposed)
            {
                return;
            }
            foreach (var handle in gcHandles)
            {
                handle.Free();
            }
            gcHandles.Clear();
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
            disposables.Clear();
            disposed = true;
        }
    }
}
