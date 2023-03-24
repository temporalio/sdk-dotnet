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
        private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(
            "binary/protobuf");

        /// <inheritdoc />
        public string Encoding => "binary/protobuf";

        /// <inheritdoc />
        public bool TryToPayload(object? value, out Payload? payload)
        {
            if (value is not IMessage proto)
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
        public object? ToValue(Payload payload, Type type)
        {
            AssertProtoPayload(payload, type);
            var message = Activator.CreateInstance(type)!;
            ((IMessage)message).MergeFrom(payload.Data);
            return message;
        }

        /// <summary>
        /// Confirm payload is the proto and return descriptor.
        /// </summary>
        /// <param name="payload">Payload to check.</param>
        /// <param name="type">Proto message type.</param>
        /// <returns>Proto descriptor.</returns>
        /// <exception cref="ArgumentException">If payload is invalid.</exception>
        internal static MessageDescriptor AssertProtoPayload(Payload payload, Type type)
        {
            if (!typeof(IMessage).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Payload is protobuf message, but type is {type}");
            }
            // TODO(cretz): Can this be done better/cheaper?
            if (type.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) is not MessageDescriptor desc)
            {
                throw new ArgumentException(
                    $"Protobuf type {type} does not have expected Descriptor static field");
            }
            if (payload.Metadata.TryGetValue("messageType", out var messageTypeBytes))
            {
                var messageType = messageTypeBytes.ToStringUtf8();
                if (messageType != desc.FullName)
                {
                    throw new ArgumentException(
                        $"Payload has protobuf message type {messageType} "
                            + "but given type's message type is {desc.FullName}");
                }
            }
            else
            {
                throw new ArgumentException("Payload missing message type");
            }
            return desc;
        }
    }
}
