using System.Text.Json;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Encoding converter for all objects via JSON.
    /// </summary>
    public class JsonPlainConverter : IEncodingConverter
    {
        private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(
            "json/plain"
        );

        /// <inheritdoc />
        public string Encoding => "json/plain";

        /// <summary>
        /// Serializer options used during conversion.
        /// </summary>
        protected JsonSerializerOptions SerializerOptions { get; private init; }

        /// <summary>
        /// Create a default JSON converter.
        /// </summary>
        public JsonPlainConverter() : this(new()) { }

        /// <summary>
        /// Create a JSON converter with the given serializer options.
        /// </summary>
        public JsonPlainConverter(JsonSerializerOptions serializerOptions)
        {
            SerializerOptions = serializerOptions;
        }

        /// <inheritdoc />
        public bool TryToPayload(object? value, out Payload? payload)
        {
            payload = new();
            payload.Metadata["encoding"] = EncodingByteString;
            // We'll just let serialization failure bubble out
            payload.Data = ByteString.CopyFrom(
                JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions)
            );
            return true;
        }

        /// <inheritdoc />
        public T? ToValue<T>(Payload payload)
        {
            return JsonSerializer.Deserialize<T>(payload.Data.ToByteArray(), SerializerOptions);
        }
    }
}
