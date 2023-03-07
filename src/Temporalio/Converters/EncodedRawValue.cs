using System;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Implementation of a <see cref="IEncodedRawValue" />.
    /// </summary>
    /// <param name="DataConverter">Data converter to perform conversions.</param>
    /// <param name="Payload">Raw payload.</param>
    public record EncodedRawValue(DataConverter DataConverter, Payload Payload) : IEncodedRawValue
    {
        /// <inheritdoc />
        public Task<T> ToValueAsync<T>() => DataConverter.ToValueAsync<T>(Payload);

        /// <inheritdoc />
        public Task<object?> ToValueAsync(Type type) => DataConverter.ToValueAsync(Payload, type);
    }
}