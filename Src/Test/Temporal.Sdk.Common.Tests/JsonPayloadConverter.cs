using System;
using Google.Protobuf;
using Newtonsoft.Json;
using Temporal.Serialization;
using Payload = Temporal.Api.Common.V1.Payload;

namespace Temporal.Sdk.Common.Tests
{
    internal class JsonPayloadConverter : IPayloadConverter
    {
        public bool TryDeserialize<T>(Api.Common.V1.Payloads serializedData, out T item)
        {
            item = JsonConvert.DeserializeObject<T>(serializedData.Payloads_[0].Data.ToStringUtf8());
            return true;
        }

        public bool TrySerialize<T>(T item, Api.Common.V1.Payloads serializedDataAccumulator)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            serializedDataAccumulator.Payloads_.Add(
                new Payload
                {
                    Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(item)),
                });

            return true;
        }
    }
}