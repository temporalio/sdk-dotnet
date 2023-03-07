using System;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Implementation of a <see cref="IRawValue" />.
    /// </summary>
    /// <param name="PayloadConverter">Payload converter to perform conversions.</param>
    /// <param name="Payload">Raw payload.</param>
    public record RawValue(IPayloadConverter PayloadConverter, Payload Payload) : IRawValue
    {
        /// <inheritdoc />
        public T ToValue<T>() => PayloadConverter.ToValue<T>(Payload);

        /// <inheritdoc />
        public object? ToValue(Type type) => PayloadConverter.ToValue(Payload, type);
    }
}