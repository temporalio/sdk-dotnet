using System;
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
            "json/plain");

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPlainConverter"/> class.
        /// </summary>
        /// <param name="serializerOptions">Serializer options.</param>
        public JsonPlainConverter(JsonSerializerOptions serializerOptions) =>
            SerializerOptions = serializerOptions;

        /// <inheritdoc />
        public string Encoding => "json/plain";

        /// <summary>
        /// Gets the serializer options used during conversion.
        /// </summary>
        protected JsonSerializerOptions SerializerOptions { get; private init; }

        /// <inheritdoc />
        public bool TryToPayload(object? value, out Payload? payload)
        {
            payload = new();
            payload.Metadata["encoding"] = EncodingByteString;
            // We'll just let serialization failure bubble out
            payload.Data = ByteString.CopyFrom(
                JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions));
            return true;
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type) =>
            JsonSerializer.Deserialize(payload.Data.ToByteArray(), type, SerializerOptions);
    }
}
