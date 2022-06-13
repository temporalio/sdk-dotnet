using System;
using Google.Protobuf;
using Temporal.Api.Common.V1;
using Temporal.Serialization;
using Xunit;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    public class NullPayloadConverterTest
    {
        [Fact]
        public void TryDeserialize_Nullable_Type()
        {
            const string NullPayloadConverterMetadataEncodingValue = "binary/null";

            ByteString bs = null;
            NullPayloadConverter instance = new();
            ByteString b = PayloadConverter.GetOrCreateBytes(NullPayloadConverterMetadataEncodingValue,
                                                             ref bs);
            Payload p = new() { Metadata = { { PayloadConverter.PayloadMetadataEncodingKey, b } } };
            Assert.True(instance.TryDeserialize(
                new Payloads { Payloads_ = { p }, },
                out string s));
            Assert.Null(s);
        }

        [Fact]
        public void TryDeserialize_Nonnullable_Type()
        {
            NullPayloadConverter instance = new();
            Assert.False(instance.TryDeserialize(new Payloads { Payloads_ = { new Payload() }, },
                out int _));
        }

        [Fact]
        public void TryDeserialize_MultiplePayloads()
        {
            NullPayloadConverter instance = new();
            Assert.False(instance.TryDeserialize(
                new Payloads { Payloads_ = { new Payload(), new Payload() }, },
                out string _));
        }

        [Fact]
        public void TryDeserialize_NoPayload()
        {
            NullPayloadConverter instance = new();
            Assert.False(instance.TryDeserialize(new(), out string _));
        }

        [Fact]
        public void TrySerialize_Null()
        {
            NullPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize<string>(null, p));
            Assert.NotEmpty(p.Payloads_);
        }

        [Fact]
        public void TrySerialize_Null_ValueType()
        {
            NullPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize<int?>(null, p));
            Assert.NotEmpty(p.Payloads_);
        }

        [Fact]
        public void TrySerialize_ValueType_Null()
        {
            NullPayloadConverter instance = new();
            Payloads p = new();
            Assert.False(instance.TrySerialize(0, p));
            Assert.Empty(p.Payloads_);
        }

        [Fact]
        public void TrySerialize_Not_Null()
        {
            NullPayloadConverter instance = new();
            Payloads p = new();
            Assert.False(instance.TrySerialize<string>(String.Empty, p));
            Assert.Empty(p.Payloads_);
        }
    }
}