using System;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Worker;

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

        /// <inheritdoc />
        public Payload ToPayload(object? value)
        {
            // TODO: Scope the generated System Nexus support converter context here once the
            // generated support file is ingested into the SDK.
            var payload = OuterPayloadConverter.ToPayload(value);
            SystemNexusPayloadVisitor.MarkSystemPayload(payload);
            return payload;
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            return OuterPayloadConverter.ToValue(payload, type);
        }
    }
}
