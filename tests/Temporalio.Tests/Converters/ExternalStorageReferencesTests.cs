namespace Temporalio.Tests.Converters;

using System.Collections.Generic;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Sdk.V1;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class ExternalStorageReferencesTests : TestBase
{
    private static readonly JsonParser ProtoJsonParser = new(
        JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    public ExternalStorageReferencesTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void ReferenceMessageType_Always_MatchesCrossSdkValue()
    {
        // Hardcoded rather than derived, because this string is on the wire. A proto rename must
        // fail here, not silently in prod.
        Assert.Equal(
            "temporal.api.sdk.v1.ExternalStorageReference",
            ExternalStorageReferences.ReferenceMessageType);
    }

    [Fact]
    public void CreateReferencePayload_Reference_ProducesExpectedWireFormat()
    {
        var payload = ExternalStorageReferences.CreateReferencePayload(
            "my-driver",
            new Dictionary<string, string> { ["bucket"] = "b", ["key"] = "k" },
            originalSizeBytes: 385);

        Assert.Equal("json/protobuf", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal(
            "temporal.api.sdk.v1.ExternalStorageReference",
            payload.Metadata["messageType"].ToStringUtf8());
        var details = Assert.Single(payload.ExternalPayloads);
        Assert.Equal(385, details.SizeBytes);

        var reference = ProtoJsonParser.Parse<ExternalStorageReference>(
            payload.Data.ToStringUtf8());
        Assert.Equal("my-driver", reference.DriverName);
        Assert.Equal("b", reference.ClaimData["bucket"]);
        Assert.Equal("k", reference.ClaimData["key"]);
    }

    [Fact]
    public void TryParseReference_CreatedReference_RoundTrips()
    {
        var payload = ExternalStorageReferences.CreateReferencePayload(
            "my-driver", new Dictionary<string, string> { ["key"] = "k" }, originalSizeBytes: 1);

        Assert.True(ExternalStorageReferences.IsReference(payload));
        Assert.True(ExternalStorageReferences.TryParseReference(payload, out var reference));
        Assert.Equal("my-driver", reference.DriverName);
        Assert.Equal("k", reference.ClaimData["key"]);
    }

    [Fact]
    public void TryParseReference_EmptyClaimData_RoundTrips()
    {
        var payload = ExternalStorageReferences.CreateReferencePayload(
            "my-driver", new Dictionary<string, string>(), originalSizeBytes: 0);

        Assert.True(ExternalStorageReferences.TryParseReference(payload, out var reference));
        Assert.Equal("my-driver", reference.DriverName);
        Assert.Empty(reference.ClaimData);
    }

    [Fact]
    public void TryParseReference_OtherSdkProtoJson_Parses()
    {
        // Taken verbatim from the Go SDK's TestClaimDeserialization_OtherSdk_ProtoJSON. Note the
        // compact, differently-ordered data JSON and the string-encoded sizeBytes: this is what
        // another SDK actually puts on the wire, which is the point of the fixture.
        const string RawPayloadJson = @"{
            ""metadata"": {
                ""encoding"": ""anNvbi9wcm90b2J1Zg=="",
                ""messageType"": ""dGVtcG9yYWwuYXBpLnNkay52MS5FeHRlcm5hbFN0b3JhZ2VSZWZlcmVuY2U=""
            },
            ""data"": ""eyJjbGFpbURhdGEiOnsiYnVja2V0IjoidGVzdC1idWNrZXQiLCJoYXNoX2FsZ29yaXRobSI6InNoYTI1NiIsImhhc2hfdmFsdWUiOiI2Y2EyMmMzNDU2MGNmMzVhYzI0NDI3ZGM3NjE5YzlhYjQ3MmE4MmNmMThmMjg2ZjI3ODcxNjQ5YTJiNTYwOGM4Iiwia2V5IjoidjAvbnMvZGVmYXVsdC93dC9MYXJnZUlPV29ya2Zsb3cvd2kvZjFkMmE0YWMtZjhjYi00NWQzLTkwOGMtOTNhMGYzM2FiMjQ1L3JpL251bGwvZC9zaGEyNTYvNmNhMjJjMzQ1NjBjZjM1YWMyNDQyN2RjNzYxOWM5YWI0NzJhODJjZjE4ZjI4NmYyNzg3MTY0OWEyYjU2MDhjOCJ9LCJkcml2ZXJOYW1lIjoiYXdzLnMzZHJpdmVyIn0="",
            ""externalPayloads"": [
                {
                    ""sizeBytes"": ""385""
                }
            ]
        }";

        var payload = ProtoJsonParser.Parse<Payload>(RawPayloadJson);

        Assert.True(ExternalStorageReferences.IsReference(payload));
        Assert.True(ExternalStorageReferences.TryParseReference(payload, out var reference));
        Assert.Equal("aws.s3driver", reference.DriverName);
        Assert.Equal("test-bucket", reference.ClaimData["bucket"]);
        Assert.Equal("sha256", reference.ClaimData["hash_algorithm"]);
        Assert.Equal(
            "v0/ns/default/wt/LargeIOWorkflow/wi/f1d2a4ac-f8cb-45d3-908c-93a0f33ab245/ri/null/d/" +
                "sha256/6ca22c34560cf35ac24427dc7619c9ab472a82cf18f286f27871649a2b5608c8",
            reference.ClaimData["key"]);
        var details = Assert.Single(payload.ExternalPayloads);
        Assert.Equal(385, details.SizeBytes);
    }

    [Fact]
    public void TryParseReference_UnknownJsonField_Parses()
    {
        var payload = new Payload()
        {
            Data = ByteString.CopyFromUtf8(
                @"{""driverName"":""my-driver"",""claimData"":{""key"":""k""},""futureField"":1}"),
        };
        payload.Metadata["encoding"] = ByteString.CopyFromUtf8("json/protobuf");
        payload.Metadata["messageType"] =
            ByteString.CopyFromUtf8("temporal.api.sdk.v1.ExternalStorageReference");

        Assert.True(ExternalStorageReferences.TryParseReference(payload, out var reference));
        Assert.Equal("my-driver", reference.DriverName);
        Assert.Equal("k", reference.ClaimData["key"]);
    }

    [Fact]
    public void TryParseReference_MalformedReferenceData_Throws()
    {
        var payload = new Payload() { Data = ByteString.CopyFromUtf8("not json") };
        payload.Metadata["encoding"] = ByteString.CopyFromUtf8("json/protobuf");
        payload.Metadata["messageType"] =
            ByteString.CopyFromUtf8("temporal.api.sdk.v1.ExternalStorageReference");

        Assert.ThrowsAny<InvalidJsonException>(() =>
            ExternalStorageReferences.TryParseReference(payload, out _));
    }

    [Theory]
    [InlineData("json/plain", null)]
    [InlineData("binary/protobuf", "temporal.api.sdk.v1.ExternalStorageReference")]
    [InlineData("json/protobuf", "temporal.api.common.v1.Payload")]
    [InlineData("json/protobuf", null)]
    [InlineData(null, "temporal.api.sdk.v1.ExternalStorageReference")]
    public void IsReference_NonReferencePayload_ReturnsFalse(
        string? encoding, string? messageType)
    {
        var payload = new Payload() { Data = ByteString.CopyFromUtf8("{}") };
        if (encoding != null)
        {
            payload.Metadata["encoding"] = ByteString.CopyFromUtf8(encoding);
        }
        if (messageType != null)
        {
            payload.Metadata["messageType"] = ByteString.CopyFromUtf8(messageType);
        }

        Assert.False(ExternalStorageReferences.IsReference(payload));
        Assert.False(ExternalStorageReferences.TryParseReference(payload, out _));
    }
}
