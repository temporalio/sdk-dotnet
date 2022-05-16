using Google.Protobuf;
using Temporal.Api.Common.V1;
using Temporal.Common;
using Temporal.Serialization;
using Xunit;
using Payload = Temporal.Api.Common.V1.Payload;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    public class TestVoidPayloadConverter
    {
        [Fact]
        [Trait("Category", "Common")]
        public void Test_VoidPayloadConverter_Empty_Payload()
        {
            VoidPayloadConverter converter = new VoidPayloadConverter();
            Assert.True(converter.TryDeserialize<IPayload.Void>(new Payloads(), out IPayload.Void value));
            Assert.NotNull(value);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_VoidPayloadConverter_Nonempty_Payload()
        {
            VoidPayloadConverter converter = new VoidPayloadConverter();
            Payloads payloads = new Payloads
            {
                Payloads_ = { { new Payload { Data = ByteString.CopyFromUtf8("hello"), } } }
            };
            Assert.False(converter.TryDeserialize<string>(payloads, out string value));
            Assert.Null(value);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_VoidPayloadConverter_Roundtrip()
        {
            VoidPayloadConverter converter = new VoidPayloadConverter();
            Payloads p = new Payloads();
            Assert.True(converter.TrySerialize(new IPayload.Void(), p));
            Assert.Empty(p.Payloads_);
            Assert.True(converter.TryDeserialize(p, out IPayload.Void item));
            Assert.NotNull(item);
        }
    }
}