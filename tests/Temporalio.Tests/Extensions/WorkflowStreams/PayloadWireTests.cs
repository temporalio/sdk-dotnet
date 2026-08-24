namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams.Internal;
using Xunit;

public class PayloadWireTests
{
    [Fact]
    public void RoundTrip_PreservesPayloadIncludingMetadata()
    {
        var payload = DataConverter.Default.PayloadConverter.ToPayload("hello");
        var wire = PayloadWire.Encode(payload);
        var decoded = PayloadWire.Decode(wire);

        Assert.Equal(payload, decoded);
        Assert.Equal(payload.Metadata, decoded.Metadata);
        Assert.Equal("hello", DataConverter.Default.PayloadConverter.ToValue<string>(decoded));
    }

    [Fact]
    public void Encode_ProducesBase64OfSerializedPayloadProto()
    {
        var payload = DataConverter.Default.PayloadConverter.ToPayload("hello");
        var wire = PayloadWire.Encode(payload);

        var parsed = Payload.Parser.ParseFrom(Convert.FromBase64String(wire));
        Assert.Equal(payload, parsed);
    }

    [Fact]
    public void Encode_MatchesCrossSdkCanonicalBytes()
    {
        var payload = new Payload
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("binary/plain") },
            Data = ByteString.CopyFromUtf8("abc"),
        };

        Assert.Equal(
            "ChgKCGVuY29kaW5nEgxiaW5hcnkvcGxhaW4SA2FiYw==",
            PayloadWire.Encode(payload));
    }

    [Fact]
    public void Decode_BadBase64_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => PayloadWire.Decode("not valid base64!!!"));

    [Fact]
    public void Decode_BadProto_ThrowsArgumentException()
    {
        var wire = Convert.ToBase64String(new byte[] { 0xff, 0xff, 0xff, 0xff });
        Assert.Throws<ArgumentException>(() => PayloadWire.Decode(wire));
    }

    [Fact]
    public void RoundTrip_BinaryPayload_Preserved()
    {
        var payload = new Payload
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("binary/plain") },
            Data = ByteString.CopyFrom(new byte[] { 0x00, 0x01, 0xff }),
        };

        var decoded = PayloadWire.Decode(PayloadWire.Encode(payload));

        Assert.Equal(payload, decoded);
        Assert.Equal(new byte[] { 0x00, 0x01, 0xff }, decoded.Data.ToByteArray());
    }

    [Fact]
    public void WireSize_SumsEncodedAndTopicLengths()
    {
        var wire = PayloadWire.Encode(DataConverter.Default.PayloadConverter.ToPayload("hello"));
        Assert.Equal(wire.Length + "topic".Length, PayloadWire.WireSize(wire, "topic"));
    }
}
