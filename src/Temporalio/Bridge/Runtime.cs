using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned runtime.
    /// </summary>
    internal class Runtime : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Runtime"/> class.
        /// </summary>
        /// <param name="options">Runtime options.</param>
        /// <exception cref="InvalidOperationException">Any internal core error.</exception>
        public Runtime(Temporalio.Runtime.TemporalRuntimeOptions options)
            : base(IntPtr.Zero, true)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    var res = Interop.Methods.runtime_new(
                        scope.Pointer(options.ToInteropOptions(scope)));
                    // If it failed, copy byte array, free runtime and byte array. Otherwise just
                    // return runtime.
                    if (res.fail != null)
                    {
                        var message = ByteArrayRef.StrictUTF8.GetString(
                            res.fail->data,
                            (int)res.fail->size);
                        Interop.Methods.byte_array_free(res.runtime, res.fail);
                        Interop.Methods.runtime_free(res.runtime);
                        throw new InvalidOperationException(message);
                    }
                    Ptr = res.runtime;
                    SetHandle((IntPtr)Ptr);
                }
            }
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the pointer to the runtime.
        /// </summary>
        internal unsafe Interop.Runtime* Ptr { get; private init; }

        /// <summary>
        /// Free a byte array.
        /// </summary>
        /// <param name="byteArray">Byte array to free.</param>
        internal unsafe void FreeByteArray(Interop.ByteArray* byteArray)
        {
            Interop.Methods.byte_array_free(Ptr, byteArray);
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.runtime_free(Ptr);
            return true;
        }
    }
}
