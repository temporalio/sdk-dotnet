using System;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Payload converter for System Nexus outer protobuf envelopes.
    /// </summary>
    /// <remarks>
    /// This converter applies transfer type conversion to the outer System Nexus envelope.
    /// </remarks>
    internal sealed class SystemNexusPayloadConverter : IPayloadConverter
    {
        private static readonly IPayloadConverter OuterPayloadConverter =
            TemporalTransferTypePayloadConverter.Wrap(
                new DefaultPayloadConverter(new BinaryProtoConverter()));

        private readonly IFailureConverter failureConverter;
        private readonly IPayloadConverter payloadConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemNexusPayloadConverter"/> class.
        /// </summary>
        /// <param name="payloadConverter">Payload converter for the envelope's embedded payloads.</param>
        /// <param name="failureConverter">Failure converter for the envelope's embedded failures.</param>
        internal SystemNexusPayloadConverter(
            IPayloadConverter payloadConverter,
            IFailureConverter failureConverter)
        {
            this.payloadConverter = payloadConverter;
            this.failureConverter = failureConverter;
        }

        /// <inheritdoc />
        public Payload ToPayload(object? value)
        {
            using var context = SystemNexusConverterContext.Push(payloadConverter, failureConverter);
            var payload = OuterPayloadConverter.ToPayload(value);
            SystemNexusPayloadVisitor.MarkSystemPayload(payload);
            return payload;
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            using var context = SystemNexusConverterContext.Push(payloadConverter, failureConverter);
            return OuterPayloadConverter.ToValue(payload, type);
        }
    }
}
