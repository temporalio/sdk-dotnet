using Temporal.Api.Common.V1;
using Temporal.Common.Payloads;
using Temporal.Serialization;
using Xunit;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    // Currently used for debug/dev
    public class TestNewtonsoftJsonPayloadConverter
    {
        [Fact]
        [Trait("Category", "Common")]
        public void Test_TestNewtonsoftJsonPayloadConverter_ValueType_Roundtrip()
        {
            NewtonsoftJsonPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(1, p));
            Assert.True(instance.TryDeserialize(p, out int actual));
            Assert.Equal(1, actual);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_TestNewtonsoftJsonPayloadConverter_ReferenceType_Roundtrip()
        {
            const string Expected = "hello";
            NewtonsoftJsonPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(Expected, p));
            Assert.True(instance.TryDeserialize(p, out string actual));
            Assert.NotNull(actual);
            Assert.Equal(Expected, actual);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_TestNewtonsoftJsonPayloadConverter_ComplexReference_Roundtrip()
        {
            SerializableClass expected = SerializableClass.Default;
            NewtonsoftJsonPayloadConverter instance = new();
            Payloads p = new();
            Assert.True(instance.TrySerialize(expected, p));
            Assert.True(instance.TryDeserialize(p, out SerializableClass actual));
            Assert.NotNull(actual);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Value, actual.Value);
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_TestNewtonsoftJsonPayloadConverter_Array_Roundtrip()
        {
            string[] expected = { "hello", "goodbye" };
            NewtonsoftJsonPayloadConverter instance = new();
            Payloads p = new();
            Assert.False(instance.TrySerialize(expected, p));
            Assert.Empty(p.Payloads_);
        }
    }
}