using Temporal.Api.Common.V1;
using Temporal.Common.Payloads;
using Temporal.Serialization;
using Temporal.TestUtil;
using Xunit;

namespace Temporal.Sdk.Common.Tests
{
    public class PayloadContainersUnnamedSerializedDataBackedTest : PayloadContainersUnnamedTestBase
    {
        public PayloadContainersUnnamedSerializedDataBackedTest()
            : base(new PayloadContainers.Unnamed.SerializedDataBacked(CreateDefaultPayloads(),
                                                                      new NewtonsoftJsonPayloadConverter()),
                   length: 1)
        {
        }

        [Fact]
        public void GetValue()
        {
            SerializableClass defaultValue = SerializableClass.CreateDefault();
            PayloadContainers.Unnamed.SerializedDataBacked instance =
                        new(CreateDefaultPayloads(), new NewtonsoftJsonPayloadConverter());
            SerializableClass value = instance.GetValue<SerializableClass>(0);
            SerializableClass.AssertEqual(defaultValue, value);
        }

        [Fact]
        public void TryGetValue()
        {
            SerializableClass defaultValue = SerializableClass.CreateDefault();
            PayloadContainers.Unnamed.SerializedDataBacked instance =
                        new(CreateDefaultPayloads(), new NewtonsoftJsonPayloadConverter());
            Assert.True(instance.TryGetValue(0, out SerializableClass value));
            SerializableClass.AssertEqual(defaultValue, value);
        }

        private static Payloads CreateDefaultPayloads()
        {
            Payloads payloads = new();
            NewtonsoftJsonPayloadConverter converter = new();
            converter.Serialize(SerializableClass.CreateDefault(), payloads);
            return payloads;
        }
    }
}