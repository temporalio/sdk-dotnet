using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Representation of a byte array owned by .NET.
    /// </summary>
    internal class ByteArrayRef
    {
        private readonly GCHandle bytesHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteArrayRef"/> class.
        /// </summary>
        /// <param name="bytes">Byte array to use.</param>
        public ByteArrayRef(byte[] bytes)
            : this(bytes, bytes.Length)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteArrayRef"/> class.
        /// </summary>
        /// <param name="bytes">Byte array to use.</param>
        /// <param name="length">Amount of bytes to use.</param>
        public ByteArrayRef(byte[] bytes, int length)
        {
            Bytes = bytes;
            bytesHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            unsafe
            {
                Ref = new Interop.ByteArrayRef()
                {
                    data = (byte*)bytesHandle.AddrOfPinnedObject(),
                    size = (UIntPtr)length,
                };
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ByteArrayRef"/> class.
        /// </summary>
        ~ByteArrayRef()
        {
            bytesHandle.Free();
        }

        /// <summary>
        /// Gets empty byte array.
        /// </summary>
        public static ByteArrayRef Empty { get; } = new(new byte[0]);

        /// <summary>
        /// Gets current byte array for this ref.
        /// </summary>
        public byte[] Bytes { get; private init; }

        /// <summary>
        /// Gets internal ref.
        /// </summary>
        public Interop.ByteArrayRef Ref { get; private init; }

        /// <summary>
        /// Gets strict UTF-8 encoding.
        /// </summary>
        internal static UTF8Encoding StrictUTF8 { get; } = new(false, true);

        /// <summary>
        /// Convert a string to a UTF-8 byte array.
        /// </summary>
        /// <param name="s">String to convert.</param>
        /// <returns>Converted byte array.</returns>
        public static ByteArrayRef FromUTF8(string s)
        {
            if (s.Length == 0)
            {
                return Empty;
            }

            return new ByteArrayRef(StrictUTF8.GetBytes(s));
        }

        /// <summary>
        /// Convert a proto to a byte array.
        /// </summary>
        /// <param name="p">Proto to convert.</param>
        /// <returns>Converted byte array.</returns>
        public static ByteArrayRef FromProto(IMessage p)
        {
            return new ByteArrayRef(p.ToByteArray());
        }

        /// <summary>
        /// Convert an enumerable set of metadata pairs to a byte array. No key or value may contain
        /// a newline.
        /// </summary>
        /// <param name="metadata">Metadata to convert.</param>
        /// <returns>Converted byte array.</returns>
        public static ByteArrayRef FromMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamWriter(stream, StrictUTF8) { AutoFlush = true };
                foreach (var pair in metadata)
                {
                    // If either have a newline, we error since it would make an invalid set
                    if (pair.Key.IndexOf('\n') >= 0 || pair.Value.IndexOf('\n') >= 0)
                    {
                        throw new ArgumentException("Metadata keys/values cannot have newlines");
                    }

                    // If the stream already has data, add another newline
                    if (stream.Length > 0)
                    {
                        writer.Write('\n');
                    }

                    writer.Write(pair.Key);
                    writer.Write('\n');
                    writer.Write(pair.Value);
                }

                if (stream.Length == 0)
                {
                    return Empty;
                }

                return new ByteArrayRef(stream.GetBuffer(), (int)stream.Length);
            }
        }

        /// <summary>
        /// Convert an enumerable set of strings to a newline-delimited byte array. No value can
        /// contain a newline.
        /// </summary>
        /// <param name="values">Values to convert.</param>
        /// <returns>Converted byte array.</returns>
        public static ByteArrayRef FromNewlineDelimited(IEnumerable<string> values)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamWriter(stream, StrictUTF8) { AutoFlush = true };
                foreach (var value in values)
                {
                    // If has a newline, we error since it would make an invalid set
                    if (value.IndexOf('\n') >= 0)
                    {
                        throw new ArgumentException("Value cannot have newline");
                    }

                    // If the stream already has data, add another newline
                    if (stream.Length > 0)
                    {
                        writer.Write('\n');
                    }

                    writer.Write(value);
                }

                if (stream.Length == 0)
                {
                    return Empty;
                }

                return new ByteArrayRef(stream.GetBuffer(), (int)stream.Length);
            }
        }
    }
}
