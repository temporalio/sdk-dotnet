namespace Temporalio.Converters
{
    /// <summary>
    /// Carries the external-storage runner and the target a payload is offloaded on behalf of into
    /// the conversion funnels. Built once per operation by the client or worker and passed to each
    /// store or retrieve call, so a call site that offloads without a target is visible rather than
    /// silently untargeted. A direct customer call uses <see cref="For"/> over the converter's own
    /// configuration, with no target.
    /// </summary>
    /// <remarks>
    /// The runner derives the driver-facing <see cref="StorageDriverStoreContext"/> from this; the
    /// target is ignored on retrieval.
    /// </remarks>
    internal sealed class ExternalStorageContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalStorageContext"/> class.
        /// </summary>
        /// <param name="runner">Runner that performs the offload and retrieval.</param>
        /// <param name="target">
        /// Execution the stored payloads are offloaded on behalf of, or null when none is known.
        /// </param>
        /// <param name="skipStore">
        /// When true, offload is skipped and payloads are left inline; retrieval is unaffected.
        /// </param>
        internal ExternalStorageContext(
            IExternalStorageRunner runner, IStorageDriverTargetInfo? target, bool skipStore = false)
        {
            Runner = runner;
            Target = target;
            SkipStore = skipStore;
        }

        /// <summary>
        /// Gets the runner that performs the offload and retrieval.
        /// </summary>
        internal IExternalStorageRunner Runner { get; }

        /// <summary>
        /// Gets the execution the stored payloads are offloaded on behalf of, or null when none is
        /// known. Not used on retrieval.
        /// </summary>
        internal IStorageDriverTargetInfo? Target { get; }

        /// <summary>
        /// Gets a value indicating whether offload is skipped, leaving payloads inline. Used when
        /// building a failure after an offload has already failed, so the codec still runs but the
        /// store is not retried. Retrieval is unaffected.
        /// </summary>
        internal bool SkipStore { get; }

        /// <summary>
        /// Create a context over the given converter's storage configuration, carrying the given
        /// target.
        /// </summary>
        /// <param name="converter">Converter whose storage configuration to offload through.</param>
        /// <param name="target">
        /// Execution the stored payloads are offloaded on behalf of, if any.
        /// </param>
        /// <param name="skipStore">
        /// When true, offload is skipped and payloads are left inline; retrieval is unaffected.
        /// </param>
        /// <returns>A context, never null.</returns>
        internal static ExternalStorageContext For(
            DataConverter converter,
            IStorageDriverTargetInfo? target = null,
            bool skipStore = false) =>
            new(ExternalStorageRunner.Create(converter.ExternalStorage), target, skipStore);
    }
}
