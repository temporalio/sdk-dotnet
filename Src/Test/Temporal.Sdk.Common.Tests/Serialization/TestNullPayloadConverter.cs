using System;
using Google.Protobuf;
using Temporal.Api.Common.V1;
using Temporal.Serialization;
using Xunit;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    public class TestNullPayloadConverter
    {
        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TryDeserialize_Nullable_Type()
        {
            ByteString bs = null;
            NullPayloadConverter instance = new NullPayloadConverter();
            ByteString b = PayloadConverter.GetOrCreateBytes(
                NullPayloadConverter.PayloadMetadataEncodingValue,
                ref bs);
            Payload p = new Payload { Metadata = { { PayloadConverter.PayloadMetadataEncodingKey, b } } };
            Assert.True(instance.TryDeserialize(
                new Payloads { Payloads_ = { p }, },
                out string s));
            Assert.Null(s);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TryDeserialize_Nonnullable_Type()
        {
            NullPayloadConverter instance = new NullPayloadConverter();
            Assert.False(instance.TryDeserialize(
                new Payloads { Payloads_ = { new Payload() }, },
                out int _));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TryDeserialize_MultiplePayloads()
        {
            NullPayloadConverter instance = new NullPayloadConverter();
            Assert.False(instance.TryDeserialize(
                new Payloads { Payloads_ = { new Payload(), new Payload() }, },
                out string _));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TryDeserialize_NoPayload()
        {
            NullPayloadConverter instance = new NullPayloadConverter();
            Assert.False(instance.TryDeserialize(new Payloads(), out string _));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TrySerialize_Null()
        {
            NullPayloadConverter instance = new NullPayloadConverter();
            Payloads p = new Payloads();
            Assert.True(instance.TrySerialize<string>(null, p));
            Assert.NotEmpty(p.Payloads_);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TrySerialize_ValueType_Null()
        {
            NullPayloadConverter instance = new NullPayloadConverter();
            Payloads p = new Payloads();
            Assert.False(instance.TrySerialize(0, p));
            Assert.Empty(p.Payloads_);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_NullPayloadConverter_TrySerialize_Not_Null()
        {
            NullPayloadConverter instance = new NullPayloadConverter();
            Payloads p = new Payloads();
            Assert.False(instance.TrySerialize<string>(String.Empty, p));
            Assert.Empty(p.Payloads_);
        }
    }
}