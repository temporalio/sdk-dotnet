using System.Threading;
using System.Threading.Tasks;
using Temporal.Api.Common.V1;

namespace Temporal.Serialization
{
    public interface IPayloadCodec
    {
        Task<Payloads> DecodeAsync(Payloads data, CancellationToken cancelToken);
        Task<Payloads> EncodeAsync(Payloads data, CancellationToken cancelToken);
    }
}
