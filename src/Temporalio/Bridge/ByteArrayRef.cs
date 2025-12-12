using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Representation of a byte array owned by .NET. Users should usually use a
    /// <see cref="Scope" /> instead of creating this directly.
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
                Ref = new Interop.TemporalCoreByteArrayRef()
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
        public static ByteArrayRef Empty { get; } = new(Array.Empty<byte>());

        /// <summary>
        /// Gets current byte array for this ref.
        /// </summary>
        public byte[] Bytes { get; private init; }

        /// <summary>
        /// Gets internal ref.
        /// </summary>
        public Interop.TemporalCoreByteArrayRef Ref { get; private init; }

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
        /// Convert a key-value pair to a byte array with key and value separated by a newline.
        /// </summary>
        /// <param name="pair">Key-value pair to convert.</param>
        /// <returns>Converted key-value pair.</returns>
        public static ByteArrayRef FromKeyValuePair(KeyValuePair<string, string> pair)
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, StrictUTF8) { AutoFlush = true })
            {
                writer.Write(pair.Key);
                writer.Write('\n');
                writer.Write(pair.Value);

                return new ByteArrayRef(stream.GetBuffer(), (int)stream.Length);
            }
        }

        /// <summary>
        /// Convert a key-value pair to a byte array with key and value separated by a newline.
        /// </summary>
        /// <param name="pair">Key-value pair to convert.</param>
        /// <returns>Converted key-value pair.</returns>
        public static ByteArrayRef FromKeyValuePair(KeyValuePair<string, byte[]> pair)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, encoding: StrictUTF8, bufferSize: -1, leaveOpen: true) { AutoFlush = true })
                {
                    writer.Write(pair.Key);
                    writer.Write('\n');
                }

                // StreamWriter does not support writing byte arrays.
                // BinaryWriter inserts extra bytes when writing strings.
                // Use stream directly to write the byte array.
                stream.Write(pair.Value, 0, pair.Value.Length);

                return new ByteArrayRef(stream.GetBuffer(), (int)stream.Length);
            }
        }

        /// <summary>
        /// Copy a byte array ref contents to a UTF8 string.
        /// </summary>
        /// <param name="byteArray">Byte array ref.</param>
        /// <returns>String.</returns>
        public static unsafe string ToUtf8(Interop.TemporalCoreByteArrayRef byteArray) =>
            StrictUTF8.GetString(byteArray.data, (int)byteArray.size);

        /// <summary>
        /// Convert an enumerable set of metadata pairs to a byte array. No key or value may contain
        /// a newline.
        /// </summary>
        /// <param name="metadata">Metadata to convert.</param>
        /// <returns>Converted byte array.</returns>
        public static ByteArrayRef FromNewlineDelimited(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, StrictUTF8) { AutoFlush = true })
            {
                foreach (var pair in metadata)
                {
                    // If either have a newline, we error since it would make an invalid set
                    if (pair.Key.Contains("\n") || pair.Value.Contains("\n"))
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
            using (var writer = new StreamWriter(stream, StrictUTF8) { AutoFlush = true })
            {
                foreach (var value in values)
                {
                    // If has a newline, we error since it would make an invalid set
                    if (value.Contains("\n"))
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
