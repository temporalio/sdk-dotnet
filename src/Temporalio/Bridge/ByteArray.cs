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
        private readonly Runtime? runtime;
        private readonly unsafe Interop.TemporalCoreByteArray* byteArray;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteArray"/> class.
        /// </summary>
        /// <param name="runtime">Runtime to use to free the byte array, or null to use no runtime.</param>
        /// <param name="byteArray">Byte array pointer.</param>
        public unsafe ByteArray(Runtime? runtime, Interop.TemporalCoreByteArray* byteArray)
            : base((IntPtr)byteArray, true)
        {
            this.runtime = runtime;
            this.byteArray = byteArray;
        }

        /// <inheritdoc/>
        public override unsafe bool IsInvalid => byteArray == null;

        /// <summary>
        /// Convert the byte array to a UTF8 string.
        /// </summary>
        /// <returns>Converted string.</returns>
        public string ToUTF8()
        {
            unsafe
            {
                return ByteArrayRef.StrictUTF8.GetString(byteArray->data, (int)byteArray->size);
            }
        }

        /// <summary>
        /// Convert the byte array to a proto message.
        /// </summary>
        /// <typeparam name="T">Proto message type.</typeparam>
        /// <param name="parser">Proto message parser to use.</param>
        /// <returns>Parsed message.</returns>
        public T ToProto<T>(MessageParser<T> parser)
            where T : IMessage<T>
        {
            unsafe
            {
                using (var stream = new UnmanagedMemoryStream(byteArray->data, (long)byteArray->size))
                {
                    return parser.ParseFrom(stream);
                }
            }
        }

        /// <summary>
        /// Copy the byte array to a new byte array.
        /// </summary>
        /// <returns>The new byte array.</returns>
        public byte[] ToByteArray()
        {
            unsafe
            {
                var bytes = new byte[(int)byteArray->size];
                Marshal.Copy((IntPtr)byteArray->data, bytes, 0, (int)byteArray->size);
                return bytes;
            }
        }

        /// <inheritdoc/>
        protected override unsafe bool ReleaseHandle()
        {
            var runtimePtr = runtime != null ? runtime.Ptr : (Interop.TemporalCoreRuntime*)null;
            Interop.Methods.temporal_core_byte_array_free(runtimePtr, byteArray);
            return true;
        }
    }
}
