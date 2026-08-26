using System;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Payload converter for System Nexus outer protobuf envelopes.
    /// </summary>
    /// <remarks>
    /// This converter applies transfer type conversion to the outer System Nexus envelope and
    /// retains the application's converters for generated conversion of embedded payloads.
    /// </remarks>
    internal sealed class SystemNexusPayloadConverter : IPayloadConverter
    {
        private static readonly IPayloadConverter OuterPayloadConverter =
            TemporalTransferTypePayloadConverter.Wrap(
                new DefaultPayloadConverter(new BinaryProtoConverter()));

        // These will scope the generated System Nexus converter context once the generated
        // support file is ingested into the SDK.
        private readonly IPayloadConverter userPayloadConverter;
        private readonly IFailureConverter userFailureConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemNexusPayloadConverter"/> class.
        /// </summary>
        /// <param name="userPayloadConverter">The application's payload converter.</param>
        /// <param name="userFailureConverter">The application's failure converter.</param>
        internal SystemNexusPayloadConverter(
            IPayloadConverter userPayloadConverter,
            IFailureConverter userFailureConverter)
        {
            this.userPayloadConverter = userPayloadConverter;
            this.userFailureConverter = userFailureConverter;
        }

        /// <inheritdoc />
        public Payload ToPayload(object? value)
        {
            // TODO: Scope userPayloadConverter and userFailureConverter in the generated System
            // Nexus support converter context once that support file is ingested into the SDK.
            return OuterPayloadConverter.ToPayload(value);
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            // TODO: Scope userPayloadConverter and userFailureConverter in the generated System
            // Nexus support converter context once that support file is ingested into the SDK.
            return OuterPayloadConverter.ToValue(payload, type);
        }
    }
}
