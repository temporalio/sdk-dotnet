using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Disposable collection of items we need to keep alive while this object is in scope. NOT threadsafe.
    /// </summary>
    internal class Scope : IDisposable
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

        private Scope()
        {
        }

        public static void WithScope(Action<Scope> fn)
        {
            using (var scope = new Scope())
            {
                fn(scope);
            }
        }

        public static T WithScope<T>(Func<Scope, T> fn)
        {
            using (var scope = new Scope())
            {
                return fn(scope);
            }
        }

        public static Task<TResult> WithScopeAsync<TResult>(
            SafeHandle handle,
            Action<WithCallback<TResult>> fn) =>
            WithScopeAsync(handle, fn, TaskCreationOptions.None);

        // TODO: Document that OnCallbackExit _must_ be run as a finally in the callback (not
        // somewhere else that feels like finally due to stack semantics in .NET about how finally
        // works), and that failure of the given lambda is expected to mean that callback wasn't
        // executed.
        public static Task<TResult> WithScopeAsync<TResult>(
            SafeHandle handle,
            Action<WithCallback<TResult>> fn,
            TaskCreationOptions creationOptions)
        {
            var completion = new TaskCompletionSource<TResult>();
#pragma warning disable CA2000 // We are delegating dispose to the caller unfortunately
            var scope = new WithCallback<TResult>(handle, completion);
#pragma warning restore CA2000
            try
            {
                fn(scope);
            }
            catch
            {
                // We assume the user was unable to run OnCallbackExit themselves
                scope.Dispose();
                throw;
            }
            return completion.Task;
        }

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

        /// <inheritdoc />
        public virtual void Dispose()
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

        public sealed class WithCallback<T> : Scope
        {
            private readonly SafeHandle? handle;
            private bool callbackPointerObtained;
            private bool disposed;

            internal WithCallback(SafeHandle handle, TaskCompletionSource<T> completion)
            {
                Completion = completion;
                bool addedRef = false;
                handle.DangerousAddRef(ref addedRef);
                if (addedRef)
                {
                    this.handle = handle;
                }
            }

            ~WithCallback()
            {
                Debug.Assert(disposed, "Callback not disposed, was OnCallbackExit called?");
            }

            public TaskCompletionSource<T> Completion { get; private init; }

            internal bool CallbackExitCalled { get; private set; }

            /// <summary>
            /// Create function pointer for delegate.
            /// </summary>
            /// <typeparam name="TCallback">Delegate type.</typeparam>
            /// <param name="func">Delegate to create pointer for.</param>
            /// <returns>Created pointer.</returns>
            public IntPtr CallbackPointer<TCallback>(TCallback func)
                where TCallback : Delegate
            {
                if (callbackPointerObtained)
                {
                    throw new InvalidOperationException("Callback already obtained");
                }
                callbackPointerObtained = true;

                // The delegate seems to get collected before called sometimes even if we add "func"
                // to the keep alive list. Delegates are supposed to be reference types, but their
                // pointers seem unstable. So we're going to alloc a handle for it. We can't pin it
                // though.
                var handle = GCHandle.Alloc(func);
                gcHandles.Add(handle);
                return Marshal.GetFunctionPointerForDelegate(handle.Target!);
            }

            public void OnCallbackExit()
            {
                Debug.Assert(!CallbackExitCalled, "Callback exit called multiple times");
                CallbackExitCalled = true;
                Dispose();
            }

            public override void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    handle?.DangerousRelease();
                }
                base.Dispose();
            }
        }
    }
}
