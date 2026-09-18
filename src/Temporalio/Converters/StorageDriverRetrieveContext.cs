namespace Temporalio.Converters
{
    /// <summary>
    /// Context given to <see cref="IStorageDriver.RetrieveAsync" />.
    /// </summary>
    /// <param name="Limiter">Limiter the driver must run each individual retrieve request through,
    /// so that its fan-out counts against the configured limits.</param>
    /// <remarks>
    /// This deliberately carries no target. A payload must be retrievable from its
    /// <see cref="StorageDriverClaim" /> alone, since the same payload can be read from a different
    /// execution than the one that stored it.
    /// </remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    internal sealed record StorageDriverRetrieveContext(
        IStorageDriverLimiter<StorageDriverClaim> Limiter);
}
