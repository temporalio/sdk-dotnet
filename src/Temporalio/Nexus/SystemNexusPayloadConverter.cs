using System;
using Temporalio.Converters;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Payload converter for System Nexus outer protobuf envelopes.
    /// </summary>
    /// <remarks>
    /// This converter applies transfer type conversion to the outer System Nexus envelope while
    /// making the application's converters available to generated transfer types.
    /// </remarks>
    internal sealed class SystemNexusPayloadConverter : IPayloadConverter
    {
        private readonly IPayloadConverter userPayloadConverter;
        private readonly IFailureConverter userFailureConverter;
        private readonly IPayloadConverter outerPayloadConverter;

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
            outerPayloadConverter = TemporalTransferTypePayloadConverter.Wrap(
                new DefaultPayloadConverter(new BinaryProtoConverter()));
        }

        /// <inheritdoc />
        public Temporalio.Api.Common.V1.Payload ToPayload(object? value)
        {
            using var context = SystemNexusConverterContext.Push(
                userPayloadConverter, userFailureConverter);
            return outerPayloadConverter.ToPayload(value);
        }

        /// <inheritdoc />
        public object? ToValue(Temporalio.Api.Common.V1.Payload payload, Type type)
        {
            using var context = SystemNexusConverterContext.Push(
                userPayloadConverter, userFailureConverter);
            return outerPayloadConverter.ToValue(payload, type);
        }
    }
}
