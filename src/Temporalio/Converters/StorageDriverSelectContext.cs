namespace Temporalio.Converters
{
    /// <summary>
    /// Context given to a <see cref="StorageDriverSelector" />.
    /// </summary>
    /// <param name="Target">The execution the payload is being stored on behalf of, or null if
    /// there is no associated execution.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    internal sealed record StorageDriverSelectContext(IStorageDriverTargetInfo? Target);
}
