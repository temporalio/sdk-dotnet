using Temporal.Common.Payloads;
using Xunit;

namespace Temporal.Sdk.Common.Tests
{
    public class TestPayloadContainersUnnamedInstanceBacked : AbstractUnnamedTest
    {
        private const string DefaultValue = "hello";
        public TestPayloadContainersUnnamedInstanceBacked()
            : base(new PayloadContainers.Unnamed.InstanceBacked<string>(new[] { DefaultValue }), 1)
        {
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_Payload_Containers_Unnamed_Instance_Backed_Type_Conversion()
        {
            PayloadContainers.Unnamed.InstanceBacked<string> instance = new PayloadContainers.Unnamed.InstanceBacked<string>(new[] { DefaultValue });
            string value = instance.GetValue<string>(0);
            Assert.Equal(DefaultValue, value);
            Assert.True(instance.TryGetValue(0, out value));
            Assert.Equal(DefaultValue, value);
        }
    }
}