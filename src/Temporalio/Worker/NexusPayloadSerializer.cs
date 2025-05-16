using System;
using System.Threading.Tasks;
using Google.Protobuf;
using NexusRpc;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    internal class NexusPayloadSerializer : ISerializer
    {
        private readonly DataConverter dataConverter;

        public NexusPayloadSerializer(DataConverter dataConverter) =>
            this.dataConverter = dataConverter;

        public async Task<ISerializer.Content> SerializeAsync(object? value)
        {
            var payload = await dataConverter.ToPayloadAsync(value).ConfigureAwait(false);
            return new(payload.ToByteArray());
        }

        public async Task<object?> DeserializeAsync(ISerializer.Content content, Type type)
        {
            var payload = Payload.Parser.ParseFrom(content.Data);
            return await dataConverter.ToValueAsync(payload, type).ConfigureAwait(false);
        }
    }
}