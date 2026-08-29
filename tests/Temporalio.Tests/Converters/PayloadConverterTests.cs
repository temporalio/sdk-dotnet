namespace Temporalio.Tests.Converters;

using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class PayloadConverterTests : TestBase
{
    public PayloadConverterTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void ToPayload_Common_Succeeds()
    {
        // Null
        AssertPayload(null, "binary/null", string.Empty);

        // Byte array
        AssertPayload(Encoding.ASCII.GetBytes("some binary"), "binary/plain", "some binary");

        // Proto
        var proto = new Temporalio.Api.Common.V1.WorkflowExecution()
        {
            WorkflowId = "id1",
            RunId = "id2",
        };
        var payload = AssertPayload(
            proto,
            "json/protobuf",
            expectedJson: "{\"workflowId\":\"id1\",\"runId\":\"id2\"}");
        Assert.Equal(
            "temporal.api.common.v1.WorkflowExecution",
            payload.Metadata["messageType"].ToStringUtf8());

        // Binary proto (i.e. w/ JSON proto removed)
        payload = AssertPayload(
            proto,
            "binary/protobuf",
            expectedBytes: proto.ToByteArray(),
            converterTypeOverride: typeof(NoJsonProtoPayloadConverter));
        Assert.Equal(
            "temporal.api.common.v1.WorkflowExecution",
            payload.Metadata["messageType"].ToStringUtf8());

        // JSON
        AssertPayload(
            new Dictionary<string, string>() { ["foo"] = "bar", ["baz"] = "qux" },
            "json/plain",
            expectedJson: "{\"baz\":\"qux\",\"foo\":\"bar\"}");
        AssertPayload("somestr", "json/plain", "\"somestr\"");
        AssertPayload(1234, "json/plain", "1234");
        AssertPayload(12.34, "json/plain", "12.34");
        AssertPayload(true, "json/plain", "true");
        AssertPayload(false, "json/plain", "false");
        // We have to disable decode value check here because .NET decoded unknown types, even
        // primitives, to JsonElement which doesn't have equality check.
        // We have to do decode check as JSON because .NET decodes unknown types not into their
        // primitive values like string, but instead into JsonElement. So this decodes into
        // []object{JsonElement, JsonElement} which fails equality.
        // TODO(cretz): Make sure to document this known .NET behavior in data conversion README
        AssertPayload(
            new object[] { "somestr", 1234 },
            "json/plain",
            expectedJson: "[\"somestr\",1234]",
            decodeValueCheckAsJson: true);
        AssertPayload(
            new SomeClass(1234, "foo"),
            "json/plain",
            expectedJson: "{\"SomeInt\":1234,\"someString\":\"foo\"}");

        // JSON with custom serializer options
        AssertPayload(
            new SomeClass(1234, "foo"),
            "json/plain",
            expectedJson: "{\"someInt\":1234,\"someString\":\"foo\"}",
            converterTypeOverride: typeof(CamelCasePayloadConverter));
    }

    [Fact]
    public void ToPayload_Common_Fails()
    {
        // Not serializable
        var action = ToPayload_Common_Fails;
        Assert.Throws<NotSupportedException>(() => AssertPayload(action, "json/plain"));
    }

    [Fact]
    public void ToValue_WrongProtoType_Fails()
    {
        var proto = new WorkflowType { Name = "WorkflowName" };
        var payload = AssertPayload(
            proto,
            "json/protobuf",
            expectedJson: "{\"name\":\"WorkflowName\"}");
        IPayloadConverter converter = DataConverter.Default.PayloadConverter;
        Assert.Equal(proto, converter.ToValue(payload, typeof(WorkflowType)));
        var e = Assert.Throws<ArgumentException>(() => converter.ToValue(payload, typeof(ActivityType)));
        Assert.Contains(WorkflowType.Descriptor.FullName, e.Message);
        Assert.Contains(ActivityType.Descriptor.FullName, e.Message);
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_Succeed()
    {
        var dataConverter = TemporalTransferTypePayloadConverter.Wrap(new DataConverter(
            new ContextStringPayloadConverter(),
            new DefaultFailureConverter())).WithSerializationContext(
                new ISerializationContext.Workflow("default", "workflow-id"));
        var value = new TransferTypeHookValue("payload-value");

        var payload = dataConverter.PayloadConverter.ToPayload(value);
        Assert.Equal("workflow-id:payload-value", payload.Data.ToStringUtf8());
        Assert.Equal(
            value,
            dataConverter.PayloadConverter.ToValue(payload, typeof(TransferTypeHookValue)));
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_DoesNotUseInheritedAttribute()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new ContextStringPayloadConverter());

        var payload = converter.ToPayload(new DerivedTransferTypeHookValue("payload-value"));

        Assert.NotEqual("test/context-string", payload.Metadata["encoding"].ToStringUtf8());
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_BaseTypeWithAttribute_Succeed()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new ContextStringPayloadConverter());
        var value = new BaseTransferTypeHookValue("payload-value");

        var payload = converter.ToPayload(value);

        Assert.Equal(":base:payload-value", payload.Data.ToStringUtf8());
        Assert.Equal(value, converter.ToValue(payload, typeof(BaseTransferTypeHookValue)));
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_DerivedTypeWithAttribute_Succeed()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new ContextStringPayloadConverter());
        var value = new AnnotatedDerivedTransferTypeHookValue("payload-value");

        var payload = converter.ToPayload(value);

        Assert.Equal(":derived:payload-value", payload.Data.ToStringUtf8());
        Assert.Equal(value, converter.ToValue(payload, typeof(AnnotatedDerivedTransferTypeHookValue)));
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_GenericConverter_Succeeds()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new DefaultPayloadConverter());
        var stringValue = new GenericTransferTypeHookValue<string>("payload-value");
        var intValue = new GenericTransferTypeHookValue<int>(1234);

        var stringPayload = converter.ToPayload(stringValue);
        var intPayload = converter.ToPayload(intValue);

        Assert.Equal(
            stringValue,
            converter.ToValue(stringPayload, typeof(GenericTransferTypeHookValue<string>)));
        Assert.Equal(
            intValue,
            converter.ToValue(intPayload, typeof(GenericTransferTypeHookValue<int>)));
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_OpenGenericConverterPreservesArgumentOrder_Succeeds()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new DefaultPayloadConverter());
        var value = new OrderedGenericTransferTypeHookValue<string, int>("payload-value", 1234);

        var payload = converter.ToPayload(value);

        Assert.Equal(
            value,
            converter.ToValue(
                payload,
                typeof(OrderedGenericTransferTypeHookValue<string, int>)));
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_ClosedConverterOnGenericType_Succeeds()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new DefaultPayloadConverter());
        var value = new ClosedConverterGenericTransferTypeHookValue<int>("payload-value");

        var payload = converter.ToPayload(value);

        Assert.Equal(
            value,
            converter.ToValue(
                payload,
                typeof(ClosedConverterGenericTransferTypeHookValue<int>)));
    }

    [Fact]
    public void ToPayload_TransferTypeHooks_NestedOpenGenericConverter_Succeeds()
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new DefaultPayloadConverter());
        var value = new NestedGenericTransferTypeHookValue<int>(1234);

        var payload = converter.ToPayload(value);

        Assert.Equal(
            value,
            converter.ToValue(payload, typeof(NestedGenericTransferTypeHookValue<int>)));
    }

    [Theory]
    [InlineData(typeof(NonAssignableConverterHookValue), "does not implement")]
    [InlineData(typeof(AbstractConverterHookValue), "abstract")]
    [InlineData(typeof(ParameterConstructorConverterHookValue), "public parameterless constructor")]
    [InlineData(typeof(PrivateConstructorConverterHookValue), "public parameterless constructor")]
    public void ToPayload_TransferTypeHooks_InvalidConverterType_Fails(
        Type type,
        string expectedMessage)
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new DefaultPayloadConverter());
        var value = Activator.CreateInstance(type, "payload-value");

        var exception = Assert.Throws<InvalidOperationException>(() => converter.ToPayload(value));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(
        typeof(OpenGenericConverterHookValue),
        typeof(GenericTransferTypeHookValueConverter<>),
        "not a closed constructed generic type")]
    [InlineData(
        typeof(GenericArityMismatchConverterHookValue<string>),
        typeof(OrderedGenericTransferTypeHookValueConverter<,>),
        "different generic arities")]
    [InlineData(
        typeof(GenericConstraintConverterHookValue<string>),
        typeof(StructGenericTransferTypeHookValueConverter<>),
        "do not satisfy the converter constraints")]
    public void ToPayload_TransferTypeHooks_InvalidGenericConverterType_Fails(
        Type type,
        Type declaredConverterType,
        string expectedMessage)
    {
        var converter = TemporalTransferTypePayloadConverter.Wrap(new DefaultPayloadConverter());
        var value = Activator.CreateInstance(type, "payload-value");

        var exception = Assert.Throws<InvalidOperationException>(() => converter.ToPayload(value));

        Assert.Contains(expectedMessage, exception.Message);
        Assert.Contains(type.ToString(), exception.Message);
        Assert.Contains(declaredConverterType.ToString(), exception.Message);
    }

    private static Payload AssertPayload(
        object? value,
        string expectedEncoding,
        string? expectedDataString = null,
        string? expectedJson = null,
        byte[]? expectedBytes = null,
        bool decodeValueCheckAsJson = false,
        Type? converterTypeOverride = null)
    {
        IPayloadConverter converter = DataConverter.Default.PayloadConverter;
        if (converterTypeOverride != null)
        {
            converter = (IPayloadConverter)Activator.CreateInstance(converterTypeOverride)!;
        }
        // Encode and check
        var payload = converter.ToPayload(value);
        Assert.Equal(expectedEncoding, payload.Metadata["encoding"].ToStringUtf8());
        if (expectedDataString != null)
        {
            Assert.Equal(expectedDataString, payload.Data.ToStringUtf8());
        }
        if (expectedJson != null)
        {
            AssertMore.JsonEqual(expectedJson, payload.Data.ToStringUtf8());
        }
        if (expectedBytes != null)
        {
            Assert.Equal(expectedBytes, payload.Data.ToByteArray());
        }

        // Decode and check
        var newValue = converter.ToValue(payload, value?.GetType() ?? typeof(object));
        if (decodeValueCheckAsJson)
        {
            var expectedValueJson = JsonSerializer.SerializeToElement(value);
            var newValueJson = JsonSerializer.SerializeToElement(newValue);
            AssertMore.JsonEqual(expectedValueJson, newValueJson);
        }
        else
        {
            Assert.Equal(value, newValue);
        }
        return payload;
    }

    public record SomeClass(
        int SomeInt,
        [property: JsonPropertyName("someString")] string? SomeString);

    public class NoJsonProtoPayloadConverter : DefaultPayloadConverter
    {
        public NoJsonProtoPayloadConverter()
            : base(
                ((DefaultPayloadConverter)DataConverter.Default.PayloadConverter).
                    EncodingConverters.Where(c => c is not JsonProtoConverter).ToArray())
        {
        }
    }

    public class CamelCasePayloadConverter : DefaultPayloadConverter
    {
        public CamelCasePayloadConverter()
            : base(
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        {
        }
    }

    [TemporalTransferTypeConverter(typeof(TransferTypeHookValueConverter))]
    public sealed record TransferTypeHookValue(string Value);

    public class TransferTypeHookValueConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object ToTransferType(object? value) => ((TransferTypeHookValue)value!).Value;

        public object FromTransferType(object? transferType) =>
            new TransferTypeHookValue((string)transferType!);
    }

    [TemporalTransferTypeConverter(typeof(BaseTransferTypeHookValueConverter))]
    public record BaseTransferTypeHookValue(string Value);

    public record DerivedTransferTypeHookValue(string Value) :
        BaseTransferTypeHookValue(Value);

    [TemporalTransferTypeConverter(typeof(AnnotatedDerivedTransferTypeHookValueConverter))]
    public record AnnotatedDerivedTransferTypeHookValue(string Value) :
        BaseTransferTypeHookValue(Value);

    public class BaseTransferTypeHookValueConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object ToTransferType(object? value) =>
            $"base:{((BaseTransferTypeHookValue)value!).Value}";

        public object FromTransferType(object? transferType) =>
            new BaseTransferTypeHookValue(((string)transferType!)[5..]);
    }

    public class AnnotatedDerivedTransferTypeHookValueConverter :
        ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object ToTransferType(object? value) =>
            $"derived:{((AnnotatedDerivedTransferTypeHookValue)value!).Value}";

        public object FromTransferType(object? transferType) =>
            new AnnotatedDerivedTransferTypeHookValue(((string)transferType!)[8..]);
    }

    [TemporalTransferTypeConverter(typeof(string))]
    public sealed record NonAssignableConverterHookValue(string Value);

    [TemporalTransferTypeConverter(typeof(AbstractTransferTypeHookValueConverter))]
    public sealed record AbstractConverterHookValue(string Value);

    public abstract class AbstractTransferTypeHookValueConverter :
        ITemporalTransferTypeConverter
    {
        public abstract Type TransferType { get; }

        public abstract object? ToTransferType(object? value);

        public abstract object? FromTransferType(object? transferType);
    }

    [TemporalTransferTypeConverter(typeof(GenericTransferTypeHookValueConverter<>))]
    public sealed record GenericTransferTypeHookValue<T>(T Value);

    public class GenericTransferTypeHookValueConverter<T> : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(T);

        public object? ToTransferType(object? value) =>
            ((GenericTransferTypeHookValue<T>)value!).Value;

        public object? FromTransferType(object? transferType) =>
            new GenericTransferTypeHookValue<T>((T)transferType!);
    }

    [TemporalTransferTypeConverter(typeof(OrderedGenericTransferTypeHookValueConverter<,>))]
    public sealed record OrderedGenericTransferTypeHookValue<TFirst, TSecond>(
        TFirst First,
        TSecond Second);

    public class OrderedGenericTransferTypeHookValueConverter<TFirst, TSecond> :
        ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object? ToTransferType(object? value)
        {
            var genericValue = (OrderedGenericTransferTypeHookValue<TFirst, TSecond>)value!;
            return $"{genericValue.First}|{genericValue.Second}";
        }

        public object? FromTransferType(object? transferType)
        {
            var values = ((string)transferType!).Split('|');
            return new OrderedGenericTransferTypeHookValue<TFirst, TSecond>(
                (TFirst)Convert.ChangeType(values[0], typeof(TFirst)),
                (TSecond)Convert.ChangeType(values[1], typeof(TSecond)));
        }
    }

    [TemporalTransferTypeConverter(typeof(ClosedGenericTransferTypeHookValueConverter))]
    public sealed record ClosedConverterGenericTransferTypeHookValue<T>(string Value);

    public class ClosedGenericTransferTypeHookValueConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object? ToTransferType(object? value) =>
            ((ClosedConverterGenericTransferTypeHookValue<int>)value!).Value;

        public object? FromTransferType(object? transferType) =>
            new ClosedConverterGenericTransferTypeHookValue<int>((string)transferType!);
    }

    [TemporalTransferTypeConverter(typeof(NestedGenericTransferTypeHookValue<>.Converter))]
    public sealed record NestedGenericTransferTypeHookValue<T>(T Value)
    {
        public class Converter : ITemporalTransferTypeConverter
        {
            public Type TransferType => typeof(T);

            public object? ToTransferType(object? value) =>
                ((NestedGenericTransferTypeHookValue<T>)value!).Value;

            public object? FromTransferType(object? transferType) =>
                new NestedGenericTransferTypeHookValue<T>((T)transferType!);
        }
    }

    [TemporalTransferTypeConverter(typeof(GenericTransferTypeHookValueConverter<>))]
    public sealed record OpenGenericConverterHookValue(string Value);

    [TemporalTransferTypeConverter(typeof(OrderedGenericTransferTypeHookValueConverter<,>))]
    public sealed record GenericArityMismatchConverterHookValue<T>(T Value);

    [TemporalTransferTypeConverter(typeof(StructGenericTransferTypeHookValueConverter<>))]
    public sealed record GenericConstraintConverterHookValue<T>(T Value);

    public class StructGenericTransferTypeHookValueConverter<T> :
        ITemporalTransferTypeConverter
        where T : struct
    {
        public Type TransferType => typeof(T);

        public object? ToTransferType(object? value) => throw new NotImplementedException();

        public object? FromTransferType(object? transferType) => throw new NotImplementedException();
    }

    [TemporalTransferTypeConverter(typeof(ParameterConstructorTransferTypeHookValueConverter))]
    public sealed record ParameterConstructorConverterHookValue(string Value);

    public class ParameterConstructorTransferTypeHookValueConverter :
        ITemporalTransferTypeConverter
    {
        public ParameterConstructorTransferTypeHookValueConverter(string value)
        {
        }

        public Type TransferType => typeof(string);

        public object? ToTransferType(object? value) => throw new NotImplementedException();

        public object? FromTransferType(object? transferType) => throw new NotImplementedException();
    }

    [TemporalTransferTypeConverter(typeof(PrivateConstructorTransferTypeHookValueConverter))]
    public sealed record PrivateConstructorConverterHookValue(string Value);

    public class PrivateConstructorTransferTypeHookValueConverter :
        ITemporalTransferTypeConverter
    {
        private PrivateConstructorTransferTypeHookValueConverter()
        {
        }

        public Type TransferType => typeof(string);

        public object? ToTransferType(object? value) => throw new NotImplementedException();

        public object? FromTransferType(object? transferType) => throw new NotImplementedException();
    }

    public class ContextStringPayloadConverter :
        IPayloadConverter,
        IWithSerializationContext<IPayloadConverter>
    {
        private static readonly DefaultPayloadConverter FallbackPayloadConverter =
            new DefaultPayloadConverter();

        private readonly string? workflowId;

        public ContextStringPayloadConverter(string? workflowId = null) =>
            this.workflowId = workflowId;

        public Payload ToPayload(object? value)
        {
            if (value is string str)
            {
                return new()
                {
                    Metadata =
                    {
                        ["encoding"] = ByteString.CopyFromUtf8("test/context-string"),
                    },
                    Data = ByteString.CopyFromUtf8($"{workflowId}:{str}"),
                };
            }
            return FallbackPayloadConverter.ToPayload(value);
        }

        public object? ToValue(Payload payload, Type type)
        {
            if (type == typeof(string) &&
                payload.Metadata["encoding"].ToStringUtf8() == "test/context-string")
            {
                var encoded = payload.Data.ToStringUtf8();
                return encoded[(encoded.IndexOf(':') + 1)..];
            }
            return FallbackPayloadConverter.ToValue(payload, type);
        }

        public IPayloadConverter WithSerializationContext(ISerializationContext context) =>
            new ContextStringPayloadConverter(((ISerializationContext.IHasWorkflow)context).WorkflowId);
    }
}
