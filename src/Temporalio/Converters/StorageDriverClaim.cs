using System.Collections.Generic;

namespace Temporalio.Converters
{
    /// <summary>
    /// Driver-specific data identifying a single payload in an external storage system.
    /// </summary>
    /// <param name="ClaimData">
    /// Key/value pairs the driver needs to retrieve the payload later. This is written into history,
    /// so it must contain everything required for retrieval and must not contain secrets.
    /// </param>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed record StorageDriverClaim(IReadOnlyDictionary<string, string> ClaimData);
}
