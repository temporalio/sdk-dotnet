using System;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Encoding converter for null values.
    /// </summary>
    public class BinaryNullConverter : IEncodingConverter
    {
        private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(
            "binary/null"
        );

        /// <inheritdoc />
        public string Encoding => "binary/null";

        /// <inheritdoc />
        public bool TryToPayload(object? value, out Payload? payload)
        {
            if (value != null)
            {
                payload = null;
                return false;
            }
            payload = new();
            payload.Metadata["encoding"] = EncodingByteString;
            return true;
        }

        /// <inheritdoc />
        public T? ToValue<T>(Payload payload)
        {
            if (payload.Data.Length > 0)
            {
                throw new ArgumentException("Expected empty data for binary/null");
            }
            var ret = default(T);
            if (ret != null)
            {
                throw new ArgumentException(
                    $"Payload is null, but type {typeof(T)} is not nullable"
                );
            }
            return ret;
        }
    }
}
