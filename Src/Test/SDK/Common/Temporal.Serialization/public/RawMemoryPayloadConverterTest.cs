using System;
using System.IO;

#if NETCOREAPP3_1_OR_GREATER
using System.Linq;
#endif

using Google.Protobuf;
using Temporal.Api.Common.V1;
using Temporal.Serialization;
using Xunit;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    public class RawMemoryPayloadConverterTest
    {
        [Fact]
        public void ByteString_Roundtrip()
        {
            ByteString bs = ByteString.CopyFrom(0, 0, 1);
            RawMemoryPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(bs, p));
            Assert.True(instance.TryDeserialize(p, out ByteString actual));
            Assert.NotNull(actual);
            Assert.Equal(3, actual.Length);
            Assert.Equal(bs.ToByteArray(), actual.ToByteArray());
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void ReadonlyMemory_Roundtrip()
        {
            Random r = new();
            byte[] buffer = new byte[10];
            r.NextBytes(buffer);
            RawMemoryPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(new ReadOnlyMemory<byte>(buffer), p));
            Assert.True(instance.TryDeserialize(p, out ReadOnlyMemory<byte> actual));
            Assert.Equal(buffer.Length, actual.Length);
            Assert.Equal(buffer.ToArray(), actual.ToArray());
        }

        [Fact]
        public void Memory_Roundtrip()
        {
            Random r = new();
            byte[] buffer = new byte[10];
            r.NextBytes(buffer);
            RawMemoryPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(buffer.AsMemory(), p));
            Assert.True(instance.TryDeserialize(p, out Memory<byte> actual));
            Assert.Equal(buffer.Length, actual.Length);
            Assert.Equal(buffer, actual.ToArray());
        }
#endif

        [Fact]
        public void MemoryStream_Roundtrip()
        {
            Random r = new();
            byte[] buffer = new byte[10];
            r.NextBytes(buffer);
            using MemoryStream ms = new(buffer);
            RawMemoryPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(ms, p));
            Assert.True(instance.TryDeserialize(p, out MemoryStream actual));
            Assert.Equal(buffer.Length, actual.Length);
            foreach (byte b in buffer)
            {
                byte read = (byte) actual.ReadByte();
                Assert.Equal(b, read);
            }
        }

        [Fact]
        public void ByteArray_Roundtrip()
        {
            Random r = new();
            byte[] buffer = new byte[10];
            r.NextBytes(buffer);
            using MemoryStream ms = new(buffer);
            RawMemoryPayloadConverter instance = new();
            Payloads p = new();
            Assert.False(instance.TrySerialize(buffer, p));
            Assert.False(instance.TryDeserialize(p, out byte[] _));
        }
    }
}