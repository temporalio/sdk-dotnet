using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    internal class ByteArrayRef
    {
        public static readonly ByteArrayRef Empty = new(new byte[0]);
        internal static readonly UTF8Encoding StrictUTF8 = new(false, true);

        public static ByteArrayRef FromUTF8(string s)
        {
            if (s.Length == 0)
            {
                return Empty;
            }
            return new ByteArrayRef(StrictUTF8.GetBytes(s));
        }

        public static ByteArrayRef FromProto(IMessage p)
        {
            return new ByteArrayRef(p.ToByteArray());
        }

        public static ByteArrayRef FromMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, StrictUTF8))
            {
                foreach (var pair in metadata)
                {
                    // If either have a newline, we error since it would make
                    // an invalid set
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
            }
            if (stream.Length == 0)
            {
                return Empty;
            }
            return new ByteArrayRef(stream.GetBuffer(), (int)stream.Length);
        }

        public static ByteArrayRef FromNewlineDelimited(IEnumerable<string> values)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, StrictUTF8))
            {
                foreach (var value in values)
                {
                    // If has a newline, we error since it would make an
                    // invalid set
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
            }
            if (stream.Length == 0)
            {
                return Empty;
            }
            return new ByteArrayRef(stream.GetBuffer(), (int)stream.Length);
        }

        public readonly byte[] bytes;
        private readonly GCHandle bytesHandle;
        public readonly Interop.ByteArrayRef _ref;

        public ByteArrayRef(byte[] bytes) : this(bytes, bytes.Length) { }

        public ByteArrayRef(byte[] bytes, int length)
        {
            this.bytes = bytes;
            bytesHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            unsafe
            {
                _ref = new Interop.ByteArrayRef()
                {
                    data = (byte*)bytesHandle.AddrOfPinnedObject(),
                    size = (UIntPtr)length,
                };
            }
        }

        public Interop.ByteArrayRef Ref => _ref;

        ~ByteArrayRef()
        {
            bytesHandle.Free();
        }
    }
}
