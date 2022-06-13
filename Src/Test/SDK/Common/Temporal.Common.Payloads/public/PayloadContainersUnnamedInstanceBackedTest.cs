using Temporal.Common.Payloads;
using Xunit;

namespace Temporal.Sdk.Common.Tests
{
    public class PayloadContainersUnnamedInstanceBackedTest : PayloadContainersUnnamedTestBase
    {
        private const string DefaultValue = "hello";

        public PayloadContainersUnnamedInstanceBackedTest()
            : base(new PayloadContainers.Unnamed.InstanceBacked<string>(new[] { DefaultValue }), 1)
        {
        }

        [Fact]
        public void GetValue()
        {
            PayloadContainers.Unnamed.InstanceBacked<string> instance = new(new[] { DefaultValue });
            string value = instance.GetValue<string>(0);
            Assert.Equal(DefaultValue, value);
        }

        [Fact]
        public void TryGetValue()
        {
            PayloadContainers.Unnamed.InstanceBacked<string> instance = new(new[] { DefaultValue });
            Assert.True(instance.TryGetValue(0, out string value));
            Assert.Equal(DefaultValue, value);
        }
    }
}