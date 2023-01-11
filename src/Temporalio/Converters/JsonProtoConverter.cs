using System;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Encoding converter for protobuf JSON data.
    /// </summary>
    public class JsonProtoConverter : IEncodingConverter
    {
        private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(
            "json/protobuf"
        );

        /// <inheritdoc />
        public string Encoding => "json/protobuf";

        /// <summary>
        /// Formatter used when converting to JSON.
        /// </summary>
        protected JsonFormatter Formatter { get; private init; }

        /// <summary>
        /// Parser used when converting from JSON.
        /// </summary>
        protected JsonParser Parser { get; private init; }

        /// <summary>
        /// Create a new default protobuf JSON converter.
        /// </summary>
        public JsonProtoConverter() : this(JsonFormatter.Default, JsonParser.Default) { }

        /// <summary>
        /// Create a new protobuf JSON converter with the given formatter and parser.
        /// </summary>
        /// <param name="formatter">Formatter used when converting to JSON.</param>
        /// <param name="parser">Parser used when converting from JSON.</param>
        public JsonProtoConverter(JsonFormatter formatter, JsonParser parser)
        {
            Formatter = formatter;
            Parser = parser;
        }

        /// <inheritdoc />
        public bool TryToPayload(object? value, out Payload? payload)
        {
            var proto = value as IMessage;
            if (proto == null)
            {
                payload = null;
                return false;
            }
            payload = new();
            payload.Metadata["encoding"] = EncodingByteString;
            payload.Metadata["messageType"] = ByteString.CopyFromUtf8(proto.Descriptor.FullName);
            payload.Data = ByteString.CopyFromUtf8(Formatter.Format(proto));
            return true;
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            var desc = BinaryProtoConverter.AssertProtoPayload(payload, type);
            return Parser.Parse(payload.Data.ToStringUtf8(), desc);
        }
    }
}
