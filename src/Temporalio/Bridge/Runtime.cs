using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    internal class Runtime : SafeHandle
    {
        internal readonly unsafe Interop.Runtime* ptr;

        public Runtime() : base(IntPtr.Zero, true)
        {
            using (var scope = new Scope()) { }
            unsafe
            {
                var options = new Interop.RuntimeOptions();
                var res = Interop.Methods.runtime_new(&options);
                GC.KeepAlive(options);
                // If it failed, copy byte array, free runtime and byte array. Otherwise just return
                // runtime.
                if (res.fail != null)
                {
                    var message = ByteArrayRef.StrictUTF8.GetString(
                        res.fail->data,
                        (int)res.fail->size
                    );
                    Interop.Methods.byte_array_free(res.runtime, res.fail);
                    Interop.Methods.runtime_free(res.runtime);
                    throw new InvalidOperationException(message);
                }
                ptr = res.runtime;
                SetHandle((IntPtr)ptr);
            }
        }

        public override unsafe bool IsInvalid => false;

        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.runtime_free(ptr);
            return true;
        }

        internal unsafe void FreeByteArray(Interop.ByteArray* byteArray)
        {
            Interop.Methods.byte_array_free(ptr, byteArray);
        }
    }
}
