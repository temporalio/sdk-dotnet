using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Worker
{
    /// <summary>
    /// Nexus serializer that delegates to Temporal data converter.
    /// </summary>
    internal class NexusPayloadSerializer : ISerializer
    {
        private readonly DataConverter dataConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusPayloadSerializer"/> class.
        /// </summary>
        /// <param name="dataConverter">Temporal data converter.</param>
        public NexusPayloadSerializer(DataConverter dataConverter) =>
            this.dataConverter = dataConverter;

        /// <inheritdoc/>
        public async Task<ISerializer.Content> SerializeAsync(object? value)
        {
            // Treat NoValue as null
            if (value is NoValue)
            {
                value = null;
            }
            var payload = await dataConverter.ToPayloadAsync(value).ConfigureAwait(false);
            return new(payload.ToByteArray());
        }

        /// <inheritdoc/>
        public async Task<object?> DeserializeAsync(ISerializer.Content content, Type type)
        {
            // As a special case, if type is NoValue, we need it to be NoValue? so it can/should be
            // serialized to null. Other SDKs treat void/absent Nexus return/param as null, but our
            // .NET "unit" type is a struct that cannot support this natively, so we change the type
            // just for the deserializer to support it, but we will ignore the result anyways later
            // in this method.
            var noValueType = type == typeof(NoValue);
            if (noValueType)
            {
                type = typeof(NoValue?);
            }

            var payload = Payload.Parser.ParseFrom(content.Data);

            // Decode with payload codec if configured. Codec failures are treated as
            // retryable INTERNAL errors since they are typically transient (e.g. a remote
            // decryption service is temporarily down).
            if (dataConverter.PayloadCodec != null)
            {
                try
                {
                    var decoded = await dataConverter.PayloadCodec.DecodeAsync(
                        new Payload[] { payload }).ConfigureAwait(false);
                    if (decoded.Count != 1)
                    {
                        throw new ArgumentException($"Expected 1 payload, found {decoded.Count}");
                    }
                    payload = decoded.First();
                }
                catch (Exception e) when (e is not ApplicationFailureException)
                {
                    throw new HandlerException(
                        HandlerErrorType.Internal,
                        "Payload codec failed to decode Nexus operation input",
                        e);
                }
            }

            // Convert with payload converter. Converter failures are non-retryable
            // BAD_REQUEST errors since the payload data doesn't match the expected type/format
            // and retrying with the same input will never succeed.
            object? result;
            try
            {
                result = dataConverter.PayloadConverter.ToValue(payload, type);
            }
            catch (Exception e) when (e is not ApplicationFailureException)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "Payload converter failed to decode Nexus operation input",
                    e,
                    HandlerErrorRetryBehavior.NonRetryable);
            }

            // Ignore result if type is NoValue. We choose to still go through the data converter
            // machinations (it will be for null value) in case user has expectations of _all_
            // inputs/outputs going through there even if null (e.g. to check encryption w/ key).
            if (noValueType)
            {
                return default(NoValue);
            }
            return result;
        }
    }
}