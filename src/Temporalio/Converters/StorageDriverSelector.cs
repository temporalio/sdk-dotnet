using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Chooses which driver stores a payload, or returns null to pass the payload through.
    /// </summary>
    /// <param name="context">Context the payload is being stored under.</param>
    /// <param name="payload">The payload being considered. Do not mutate this.</param>
    /// <returns>
    /// The driver to store the payload with, or null to pass the payload through.
    /// </returns>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal delegate IStorageDriver? StorageDriverSelector(
        StorageDriverSelectContext context, Payload payload);
}
