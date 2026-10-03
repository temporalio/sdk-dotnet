using System;
using System.Threading.Tasks;
using Google.Protobuf;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Nexus;

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
            var payload = await CreateContextualDataConverter().ToPayloadAsync(value)
                .ConfigureAwait(false);
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
            var contextualDataConverter = CreateContextualDataConverter();
            var noValueType = type == typeof(NoValue);
            if (noValueType)
            {
                type = typeof(NoValue?);
            }

            var payload = Payload.Parser.ParseFrom(content.Data);
            var isSystemPayload = SystemNexusPayloadVisitor.IsSystemPayload(payload);

            if (isSystemPayload &&
                !SystemNexusPayloadVisitor.TryGetVisitor(payload, out var messageType, out _))
            {
                throw new HandlerException(
                    HandlerErrorType.Internal,
                    $"Unrecognized System Nexus envelope message type: {messageType}",
                    errorRetryBehavior: HandlerErrorRetryBehavior.Retryable);
            }

            // Decode with payload codec if configured. Codec failures are treated as
            // retryable INTERNAL errors since they are typically transient (e.g. a remote
            // decryption service is temporarily down). Application failures and handler
            // exceptions are passed through untouched so users can control the resulting
            // Nexus error, except non-retryable payload validation failures which report
            // invalid input and therefore become non-retryable BAD_REQUEST errors.
            if (contextualDataConverter.PayloadCodec != null)
            {
                try
                {
                    await PayloadCodecHelper.DecodeAsync(
                        contextualDataConverter.PayloadCodec, payload).ConfigureAwait(false);
                }
                catch (Exception e) when (PayloadValidationError.IsException(e))
                {
                    throw new HandlerException(
                        HandlerErrorType.BadRequest,
                        "Invalid operation input",
                        e,
                        HandlerErrorRetryBehavior.NonRetryable);
                }
                catch (Exception e) when (
                    e is not ApplicationFailureException && e is not HandlerException)
                {
                    throw new HandlerException(
                        HandlerErrorType.Internal,
                        "Payload codec failed to decode Nexus operation input",
                        e);
                }
            }

            // Convert with payload converter. Converter failures are non-retryable
            // BAD_REQUEST errors since the payload data doesn't match the expected type/format
            // and retrying with the same input will never succeed. Application failures and
            // handler exceptions are passed through untouched so users can control the
            // resulting Nexus error, except non-retryable payload validation failures which
            // report invalid input and therefore become non-retryable BAD_REQUEST errors.
            object? result;
            try
            {
                var payloadConverter = isSystemPayload ?
                    new SystemNexusPayloadConverter(
                        contextualDataConverter.PayloadConverter,
                        contextualDataConverter.FailureConverter) :
                    contextualDataConverter.PayloadConverter;
                result = payloadConverter.ToValue(payload, type);
            }
            catch (Exception e) when (PayloadValidationError.IsException(e))
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "Invalid operation input",
                    e,
                    HandlerErrorRetryBehavior.NonRetryable);
            }
            catch (Exception e) when (
                e is not ApplicationFailureException && e is not HandlerException)
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

        /// <summary>
        /// Creates a data converter scoped to the operation currently being handled.
        /// </summary>
        /// <remarks>
        /// A single serializer is shared by every operation the worker handles, so the context is
        /// resolved per call from the task being handled rather than captured once. Falls back to
        /// the uncontextualized converter when there is no Nexus operation in scope, which is the
        /// case when this serializer is used directly rather than by the worker.
        /// </remarks>
        private DataConverter CreateContextualDataConverter() =>
            Temporalio.Nexus.NexusOperationExecutionContext.AsyncLocalCurrent.Value is { } ctx ?
                dataConverter.WithSerializationContext(ctx.SerializationContext) : dataConverter;
    }
}
