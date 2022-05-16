using Google.Protobuf;
using Newtonsoft.Json;
using Temporal.Api.Common.V1;
using Temporal.Common.Payloads;
using Xunit;

namespace Temporal.Sdk.Common.Tests
{
    public class TestPayloadContainersUnnamedSerializedDataBacked : AbstractUnnamedTest
    {
        public TestPayloadContainersUnnamedSerializedDataBacked()
            : base(
                new PayloadContainers.Unnamed.SerializedDataBacked(CreateDefaultPayloads(), new JsonPayloadConverter()),
                1)
        {
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_Payload_Containers_Unnamed_Instance_Backed_Type_Conversion()
        {
            ConvertedClass defaultValue = ConvertedClass.Default;
            PayloadContainers.Unnamed.SerializedDataBacked instance =
                new PayloadContainers.Unnamed.SerializedDataBacked(CreateDefaultPayloads(), new JsonPayloadConverter());
            ConvertedClass value = instance.GetValue<ConvertedClass>(0);
            AssertWellFormed(defaultValue, value);
            Assert.True(instance.TryGetValue(0, out value));
            AssertWellFormed(defaultValue, value);
        }

        private static void AssertWellFormed(ConvertedClass expected, ConvertedClass actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Value, actual.Value);
        }

        private static Payloads CreateDefaultPayloads()
        {
            return new Payloads
            {
                Payloads_ =
                {
                    new Payload
                    {
                        Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(ConvertedClass.Default)),
                    },
                },
            };
        }

        private class ConvertedClass
        {
            public string Name { get; set; }

            public int Value { get; set; }

            public static ConvertedClass Default
            {

                get
                {
                    return new ConvertedClass { Name = "Test", Value = 1, };
                }
            }
        }
    }
}