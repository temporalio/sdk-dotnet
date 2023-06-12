namespace Temporalio.Tests.Converters;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Xunit;

public class Base64PayloadCodec : IPayloadCodec
{
    public const string EncodingName = "my-encoding";

    public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            new Payload()
            {
                Data = ByteString.CopyFrom(
                    Convert.ToBase64String(p.ToByteArray()),
                    Encoding.ASCII),
                Metadata =
                {
                    new Dictionary<string, ByteString>
                    {
                        ["encoding"] = ByteString.CopyFromUtf8(EncodingName),
                    },
                },
            }).ToList());

    public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads)
    {
        return Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
        {
            Assert.Equal(EncodingName, p.Metadata["encoding"].ToStringUtf8());
            return Payload.Parser.ParseFrom(
                Convert.FromBase64String(p.Data.ToString(Encoding.ASCII)));
        }).ToList());
    }
}
