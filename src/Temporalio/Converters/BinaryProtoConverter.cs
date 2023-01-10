using System;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Encoding converter for protobuf binary data.
    /// </summary>
    public class BinaryProtoConverter : IEncodingConverter
    {
        internal static MessageDescriptor AssertProtoPayload<T>(Payload payload)
        {
            var type = typeof(T);
            if (typeof(IMessage).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Payload is protobuf message, but type is {type}");
            }
            // TODO(cretz): Can this be done better?
            var desc =
                type.GetField("Descriptor", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) as MessageDescriptor;
            if (desc == null)
            {
                throw new ArgumentException(
                    $"Protobuf type {type} does not have expected Descriptor static field"
                );
            }
            if (payload.Metadata.TryGetValue("messageType", out var messageTypeBytes))
            {
                var messageType = messageTypeBytes.ToStringUtf8();
                if (messageType != desc.FullName)
                {
                    throw new ArgumentException(
                        $"Payload has protobuf message type {messageType} "
                            + "but given type's message type is {desc.FullName}"
                    );
                }
            }
            else
            {
                throw new ArgumentException("Payload missing message type");
            }
            return desc;
        }

        private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(
            "binary/protobuf"
        );

        /// <inheritdoc />
        public string Encoding => "binary/protobuf";

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
            payload.Data = proto.ToByteString();
            return true;
        }

        /// <inheritdoc />
        public T? ToValue<T>(Payload payload)
        {
            AssertProtoPayload<T>(payload);
            var message = (T)Activator.CreateInstance(typeof(T))!;
            ((IMessage)message).MergeFrom(payload.Data);
            return message;
        }
    }
}
