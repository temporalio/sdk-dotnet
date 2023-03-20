using System;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Encoding converter for byte arrays.
    /// </summary>
    public class BinaryPlainConverter : IEncodingConverter
    {
        private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(
            "binary/plain");

        /// <inheritdoc />
        public string Encoding => "binary/plain";

        /// <inheritdoc />
        public bool TryToPayload(object? value, out Payload? payload)
        {
            if (value is not byte[] bytes)
            {
                payload = null;
                return false;
            }
            payload = new();
            payload.Metadata["encoding"] = EncodingByteString;
            payload.Data = ByteString.CopyFrom(bytes);
            return true;
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            if (!type.Equals(typeof(byte[])))
            {
                throw new ArgumentException($"Payload is byte array, but type is {type}");
            }
            return payload.Data.ToByteArray();
        }
    }
}
