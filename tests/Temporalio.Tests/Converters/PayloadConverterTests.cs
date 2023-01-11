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
    public PayloadConverterTests(ITestOutputHelper output) : base(output) { }

    public record SomeClass(
        int SomeInt,
        [property: JsonPropertyName("someString")] string? SomeString
    );

    public class NoJsonProtoPayloadConverter : PayloadConverter
    {
        public NoJsonProtoPayloadConverter()
            : base(
                DataConverter.Default.PayloadConverter.EncodingConverters.Where(
                    c => !(c is JsonProtoConverter)
                )
            ) { }
    }

    public class CamelCasePayloadConverter : PayloadConverter
    {
        public CamelCasePayloadConverter()
            : base(
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            ) { }
    }

    [Fact]
    public void ToPayload_Common_Succeeds()
    {
        // Null
        AssertPayload(null, "binary/null", "");

        // Byte array
        AssertPayload(Encoding.ASCII.GetBytes("some binary"), "binary/plain", "some binary");

        // Proto
        var proto = new Temporalio.Api.Common.V1.WorkflowExecution()
        {
            WorkflowId = "id1",
            RunId = "id2"
        };
        var payload = AssertPayload(
            proto,
            "json/protobuf",
            expectedJson: "{\"workflowId\":\"id1\",\"runId\":\"id2\"}"
        );
        Assert.Equal(
            "temporal.api.common.v1.WorkflowExecution",
            payload.Metadata["messageType"].ToStringUtf8()
        );

        // Binary proto (i.e. w/ JSON proto removed)
        payload = AssertPayload(
            proto,
            "binary/protobuf",
            expectedBytes: proto.ToByteArray(),
            converterTypeOverride: typeof(NoJsonProtoPayloadConverter)
        );
        Assert.Equal(
            "temporal.api.common.v1.WorkflowExecution",
            payload.Metadata["messageType"].ToStringUtf8()
        );

        // JSON
        AssertPayload(
            new Dictionary<string, string>() { ["foo"] = "bar", ["baz"] = "qux" },
            "json/plain",
            expectedJson: "{\"baz\":\"qux\",\"foo\":\"bar\"}"
        );
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
            decodeValueCheckAsJson: true
        );
        AssertPayload(
            new SomeClass(1234, "foo"),
            "json/plain",
            expectedJson: "{\"SomeInt\":1234,\"someString\":\"foo\"}"
        );

        // JSON with custom serializer options
        AssertPayload(
            new SomeClass(1234, "foo"),
            "json/plain",
            expectedJson: "{\"someInt\":1234,\"someString\":\"foo\"}",
            converterTypeOverride: typeof(CamelCasePayloadConverter)
        );
    }

    [Fact]
    public void ToPayload_Common_Fails()
    {
        // Not serializable
        var action = ToPayload_Common_Fails;
        Assert.Throws<NotSupportedException>(() => AssertPayload(action, "json/plain"));
    }

    private static Payload AssertPayload(
        object? value,
        string expectedEncoding,
        string? expectedDataString = null,
        string? expectedJson = null,
        byte[]? expectedBytes = null,
        bool decodeValueCheckAsJson = false,
        Type? converterTypeOverride = null
    )
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
}
