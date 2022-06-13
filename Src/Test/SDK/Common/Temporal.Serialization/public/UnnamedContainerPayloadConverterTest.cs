using System;
using Temporal.Api.Common.V1;
using Temporal.Common.Payloads;
using Temporal.Serialization;
using Temporal.TestUtil;
using Xunit;

namespace Temporal.Sdk.Common.Tests.Serialization
{
    public class UnnamedContainerPayloadConverterTest
    {
        [Fact]
        public void TrySerialize_String()
        {
            UnnamedContainerPayloadConverter instance = new();
            Payloads p = new();
            Assert.False(instance.TrySerialize(String.Empty, p));
            Assert.Empty(p.Payloads_);
        }

        [Fact]
        public void TrySerialize_Null()
        {
            UnnamedContainerPayloadConverter instance = new();
            Payloads p = new();
            Assert.False(instance.TrySerialize<string>(null, p));
            Assert.Empty(p.Payloads_);
        }

#pragma warning disable xUnit2000 // Constants and literals should be the expected argument; Why not?
        [Fact]
        public void TrySerialize_Unnamed_SerializedDataBacked()
        {
            // Create `unnamedContainerConverter`: the instance we are testing:
            UnnamedContainerPayloadConverter unnamedContainerConverter = new();
            NewtonsoftJsonPayloadConverter underlyingJsonConverter = new();
            unnamedContainerConverter.InitDelegates(unnamedContainerConverter, underlyingJsonConverter);

            // Create a serializedJson payload for the test:
            Payloads serializedJson = new();
            NewtonsoftJsonPayloadConverter jsonConverter = new();
            jsonConverter.Serialize(new SerializableClass { Name = "test", Value = 2 }, serializedJson);
            Assert.NotEmpty(serializedJson.Payloads_);
            Assert.Equal(serializedJson.Payloads_.Count, 1);

            // Create a container backed by the serializedJson payload:
            PayloadContainers.Unnamed.SerializedDataBacked serializedDataContainer = new(serializedJson, jsonConverter);
            Assert.NotSame(serializedDataContainer.PayloadConverter, unnamedContainerConverter);

            // Now validate serializing the container:
            Payloads serializedContainer = new();
            Assert.True(unnamedContainerConverter.TrySerialize(serializedDataContainer, serializedContainer));
            Assert.NotEmpty(serializedContainer.Payloads_);

            // Read the container back from its serialized form:
            Assert.True(unnamedContainerConverter.TryDeserialize(
                                    serializedContainer,
                                    out PayloadContainers.Unnamed.SerializedDataBacked roundtrippedContainer));
            Assert.NotNull(roundtrippedContainer);
            Assert.IsType<CompositePayloadConverter>(roundtrippedContainer.PayloadConverter);
            Assert.Equal(2,
                        ((CompositePayloadConverter) roundtrippedContainer.PayloadConverter).Converters.Count);
            Assert.Same(unnamedContainerConverter,
                        ((CompositePayloadConverter) roundtrippedContainer.PayloadConverter).Converters[0]);
            Assert.Same(underlyingJsonConverter,
                        ((CompositePayloadConverter) roundtrippedContainer.PayloadConverter).Converters[1]);

            // Read again, but use iface rather than the concrete class:
            Assert.True(unnamedContainerConverter.TryDeserialize(
                                    serializedContainer,
                                    out PayloadContainers.IUnnamed roundtrippedContainer2));
            Assert.NotNull(roundtrippedContainer2);
            Assert.IsType<PayloadContainers.Unnamed.SerializedDataBacked>(roundtrippedContainer2);
            PayloadContainers.Unnamed.SerializedDataBacked roundtrippedContainer2Cast
                                            = (PayloadContainers.Unnamed.SerializedDataBacked) roundtrippedContainer2;
            Assert.IsType<CompositePayloadConverter>(roundtrippedContainer2Cast.PayloadConverter);
            Assert.Equal(2,
                        ((CompositePayloadConverter) roundtrippedContainer2Cast.PayloadConverter).Converters.Count);
            Assert.Same(unnamedContainerConverter,
                        ((CompositePayloadConverter) roundtrippedContainer2Cast.PayloadConverter).Converters[0]);
            Assert.Same(underlyingJsonConverter,
                        ((CompositePayloadConverter) roundtrippedContainer2Cast.PayloadConverter).Converters[1]);

            // For both read versions, validate the contents:
            SerializableClass roundtrippedContainerItem = roundtrippedContainer.GetValue<SerializableClass>(0);
            Assert.Equal("test", roundtrippedContainerItem.Name);
            Assert.Equal(2, roundtrippedContainerItem.Value);

            SerializableClass roundtrippedContainer2Item = roundtrippedContainer2.GetValue<SerializableClass>(0);
            Assert.Equal("test", roundtrippedContainer2Item.Name);
            Assert.Equal(2, roundtrippedContainer2Item.Value);

            // @ToDo: we should test that if we re-serialize again using unnamedContainerConverter, the underlying 
            // data does not get roundtripped (original payload is used), but with a different converter is DOES
            // get roundtripped.
        }
#pragma warning restore xUnit2000 // Constants and literals should be the expected argument

        [Fact]
        public void TrySerialize_Unnamed_InstanceBacked()
        {
            static void AssertDeserialization<T>(IPayloadConverter i, Payloads p)
            {
                Assert.True(i.TryDeserialize(p, out PayloadContainers.Unnamed.SerializedDataBacked cl));
                Assert.NotEmpty(cl);
                Assert.True(cl.TryGetValue(0, out string val));
                Assert.Equal("hello", val);
            }

            UnnamedContainerPayloadConverter instance = new();
            NewtonsoftJsonPayloadConverter underlyingJsonConverter = new();
            instance.InitDelegates(instance, underlyingJsonConverter);

            Payloads p = new();
            PayloadContainers.Unnamed.InstanceBacked<string> data = new(new[] { "hello" });
            Assert.True(instance.TrySerialize(data, p));
            Assert.NotEmpty(p.Payloads_);
            Assert.False(instance.TryDeserialize(p, out PayloadContainers.Unnamed.InstanceBacked<string> _));
            AssertDeserialization<PayloadContainers.Unnamed.SerializedDataBacked>(instance, p);
            AssertDeserialization<PayloadContainers.IUnnamed>(instance, p);
        }

        [Fact]
        public void TrySerialize_Unnamed_Empty()
        {
            UnnamedContainerPayloadConverter instance = new();
            NewtonsoftJsonPayloadConverter underlyingJsonConverter = new();
            instance.InitDelegates(instance, underlyingJsonConverter);

            Payloads p = new();
            PayloadContainers.Unnamed.Empty data = new();
            Assert.True(instance.TrySerialize(data, p));
            Assert.Empty(p.Payloads_);
        }

        // @ToDo: (Future) I think it might be useful to also have a nested test case with a dataset along these lines:
        // https://github.com/temporalio/sdk-dotnet/blob/3054a2282b2eabf86d94fc970db6d4da55f8746d/Src/Demos/AdHocScenarios/Temporal.Demos.AdHocScenarios/AdHocClientInvocations.cs#L99-L108
        // 
    }
}