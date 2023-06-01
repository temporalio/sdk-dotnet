namespace Temporalio.Tests.Converters;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class PayloadCodecTests : TestBase
{
    public PayloadCodecTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task EncodeFailureAsync_Common_Succeeds()
    {
        var orig = new Failure()
        {
            EncodedAttributes = DataConverter.Default.PayloadConverter.ToPayload(12),
            ApplicationFailureInfo = new()
            {
                Details = new()
                {
                    Payloads_ = { { DataConverter.Default.PayloadConverter.ToPayload(34) } },
                },
            },
            Cause = new()
            {
                EncodedAttributes = DataConverter.Default.PayloadConverter.ToPayload(56),
                TimeoutFailureInfo = new()
                {
                    LastHeartbeatDetails = new()
                    {
                        Payloads_ = { { DataConverter.Default.PayloadConverter.ToPayload(78) } },
                    },
                },
            },
        };
        void AssertPayloadData(Payload payload, string encoding, string data)
        {
            Assert.Equal(encoding, payload.Metadata["encoding"].ToStringUtf8());
            Assert.Equal(data, payload.Data.ToStringUtf8());
        }
        void AssertPayloadNotData(Payload payload, string encoding, string data)
        {
            Assert.Equal(encoding, payload.Metadata["encoding"].ToStringUtf8());
            Assert.NotEqual(data, payload.Data.ToStringUtf8());
        }

        var encoded = new Failure(orig);
        await new Base64PayloadCodec().EncodeFailureAsync(encoded);
        AssertPayloadNotData(encoded.EncodedAttributes, "my-encoding", "12");
        AssertPayloadNotData(
            encoded.ApplicationFailureInfo.Details.Payloads_.First(),
            "my-encoding",
            "34");
        AssertPayloadNotData(encoded.Cause.EncodedAttributes, "my-encoding", "56");
        AssertPayloadNotData(
            encoded.Cause.TimeoutFailureInfo.LastHeartbeatDetails.Payloads_.First(),
            "my-encoding",
            "78");

        var decoded = new Failure(encoded);
        await new Base64PayloadCodec().DecodeFailureAsync(decoded);
        AssertPayloadData(decoded.EncodedAttributes, "json/plain", "12");
        AssertPayloadData(
            decoded.ApplicationFailureInfo.Details.Payloads_.First(),
            "json/plain",
            "34");
        AssertPayloadData(decoded.Cause.EncodedAttributes, "json/plain", "56");
        AssertPayloadData(
            decoded.Cause.TimeoutFailureInfo.LastHeartbeatDetails.Payloads_.First(),
            "json/plain",
            "78");
    }

    [Fact]
    public void PayloadCodec_Headers_AllEncoded() => throw new NotImplementedException();

    public class Base64PayloadCodec : IPayloadCodec
    {
        public Task<IEnumerable<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads)
        {
            return Task.FromResult(
                payloads.Select(
                    p =>
                        new Payload()
                        {
                            Data = ByteString.CopyFrom(
                                Convert.ToBase64String(p.ToByteArray()),
                                Encoding.ASCII),
                            Metadata =
                            {
                                new Dictionary<string, ByteString>
                                {
                                    ["encoding"] = ByteString.CopyFromUtf8("my-encoding"),
                                },
                            },
                        }));
        }

        public Task<IEnumerable<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads)
        {
            return Task.FromResult(
                payloads.Select(p =>
                {
                    Assert.Equal("my-encoding", p.Metadata["encoding"].ToStringUtf8());
                    return Payload.Parser.ParseFrom(
                        Convert.FromBase64String(p.Data.ToString(Encoding.ASCII)));
                }));
        }
    }
}
