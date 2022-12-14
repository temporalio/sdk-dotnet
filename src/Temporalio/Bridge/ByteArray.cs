using System;
using System.IO;
using System.Runtime.InteropServices;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    internal class ByteArray : SafeHandle
    {
        private readonly Runtime runtime;
        private readonly unsafe Interop.ByteArray* byteArray;

        public unsafe ByteArray(Runtime runtime, Interop.ByteArray* byteArray)
            : base((IntPtr)byteArray, true)
        {
            this.runtime = runtime;
            this.byteArray = byteArray;
        }

        public override unsafe bool IsInvalid => false;

        protected override unsafe bool ReleaseHandle()
        {
            runtime.FreeByteArray(byteArray);
            return true;
        }

        public string ToUTF8()
        {
            unsafe
            {
                return ByteArrayRef.StrictUTF8.GetString(byteArray->data, (int)byteArray->size);
            }
        }

        public T ToProto<T>(MessageParser<T> parser) where T : IMessage<T>
        {
            unsafe
            {
                return parser.ParseFrom(
                    new UnmanagedMemoryStream(byteArray->data, (long)byteArray->size)
                );
            }
        }
    }
}
