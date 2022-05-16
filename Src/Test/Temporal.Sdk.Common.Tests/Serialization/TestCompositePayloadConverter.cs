using Temporal.Api.Common.V1;
using Temporal.Common;
using Temporal.Common.Payloads;
using Temporal.Serialization;
using Xunit;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    public class TestCompositePayloadConverter
    {
        [Fact]
        [Trait("Category", "Common")]
        public void Test_CompositePayloadConverter_Void_Roundtrip()
        {
            CompositePayloadConverter converter = new CompositePayloadConverter();
            Payloads p = new Payloads();
            Assert.True(converter.TrySerialize(new IPayload.Void(), p));
            Assert.Empty(p.Payloads_);
            Assert.True(converter.TryDeserialize(p, out IPayload.Void item));
            Assert.NotNull(item);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_CompositePayloadConverter_Null_Roundtrip()
        {
            CompositePayloadConverter converter = new CompositePayloadConverter();
            Payloads p = new Payloads();
            Assert.True(converter.TrySerialize<string>(null, p));
            Assert.Single(p.Payloads_);
            Assert.True(converter.TryDeserialize(p, out string item));
            Assert.Null(item);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_CompositePayloadConverter_Unnamed_Roundtrip()
        {
            UnnamedContainerPayloadConverter unnamed = new UnnamedContainerPayloadConverter();
            unnamed.InitDelegates(new[] { new JsonPayloadConverter() });
            CompositePayloadConverter instance = new CompositePayloadConverter(new IPayloadConverter[]
            {
                new VoidPayloadConverter(),
                new NullPayloadConverter(),
                new UnnamedContainerPayloadConverter(),
                new JsonPayloadConverter(),
            });
            Payloads p = new Payloads();
            PayloadContainers.Unnamed.InstanceBacked<string> data = new PayloadContainers.Unnamed.InstanceBacked<string>(new[] { "hello" });
            Assert.True(instance.TrySerialize(data, p));
            Assert.NotEmpty(p.Payloads_);
            Assert.True(instance.TryDeserialize(p, out PayloadContainers.Unnamed.SerializedDataBacked cl));
            Assert.NotEmpty(cl);
            Assert.True(cl.TryGetValue(0, out string val));
            Assert.Equal("hello", val);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_CompositePayloadConverter_Catchall_Roundtrip()
        {
            CompositePayloadConverter converter = new CompositePayloadConverter();
            Payloads p = new Payloads();
            Assert.True(converter.TrySerialize(new IPayload.Void(), p));
            Assert.Empty(p.Payloads_);
            Assert.True(converter.TryDeserialize(p, out IPayload.Void item));
            Assert.NotNull(item);
        }
    }
}