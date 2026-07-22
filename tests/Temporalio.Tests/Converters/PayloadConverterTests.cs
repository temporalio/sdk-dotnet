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
        var dataConverter = new DataConverter(
            new ContextStringPayloadConverter(),
            new DefaultFailureConverter()).WithSerializationContext(
                new ISerializationContext.Workflow("default", "workflow-id"));
        var value = new TransferTypeHookValue("payload-value");

        var payload = dataConverter.PayloadConverter.ToPayload(value);
        Assert.Equal("workflow-id:payload-value", payload.Data.ToStringUtf8());
        Assert.Equal(
            value,
            dataConverter.PayloadConverter.ToValue(payload, typeof(TransferTypeHookValue)));
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
                new DefaultPayloadConverter().
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

    [TemporalTransferType(typeof(TransferTypeHookValueConverter))]
    public sealed record TransferTypeHookValue(string Value);

    public class TransferTypeHookValueConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object ToTransferType(object? value) => ((TransferTypeHookValue)value!).Value;

        public object FromTransferType(object? transferType) =>
            new TransferTypeHookValue((string)transferType!);
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
