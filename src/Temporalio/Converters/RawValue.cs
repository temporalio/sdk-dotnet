using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Implementation of a <see cref="IRawValue" />.
    /// </summary>
    /// <param name="Payload">Raw payload.</param>
    public record RawValue(Payload Payload) : IRawValue;
}