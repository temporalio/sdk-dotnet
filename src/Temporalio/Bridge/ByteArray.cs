using System;
using System.IO;
using System.Runtime.InteropServices;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Representation of a byte array owned by Core.
    /// </summary>
    internal class ByteArray : SafeHandle
    {
        private readonly Runtime runtime;
        private readonly unsafe Interop.ByteArray* byteArray;

        /// <summary>
        /// Instantiate this handle referencing the given pointer.
        /// </summary>
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

        /// <summary>
        /// Convert the byte array to a UTF8 string.
        /// </summary>
        public string ToUTF8()
        {
            unsafe
            {
                return ByteArrayRef.StrictUTF8.GetString(byteArray->data, (int)byteArray->size);
            }
        }

        /// <summary>
        /// Convert the byte array to a protobuf message.
        /// </summary>
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
