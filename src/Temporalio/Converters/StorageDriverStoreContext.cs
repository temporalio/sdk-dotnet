using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Context given to <see cref="IStorageDriver.StoreAsync" />.
    /// </summary>
    /// <param name="Target">The execution the payloads are being stored on behalf of, or null if
    /// there is no associated execution.</param>
    /// <param name="Limiter">Limiter the driver must run each individual store request through, so
    /// that its fan-out counts against the configured limits.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    internal sealed record StorageDriverStoreContext(
        IStorageDriverTargetInfo? Target,
        IStorageDriverLimiter<Payload> Limiter);
}
