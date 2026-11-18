namespace Temporalio.Converters
{
    /// <summary>
    /// Carries the external-storage runner and the target a payload is offloaded on behalf of into
    /// the conversion funnels. Built once per operation by the client or worker and passed to each
    /// store or retrieve call, so a call site that offloads without a target is visible rather than
    /// silently untargeted. A direct customer call uses <see cref="For(DataConverter, bool)"/> over
    /// the converter's own configuration, with no namespace or target.
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
        /// Create a context over the given converter's storage configuration for an operation that
        /// is not performed by a client or worker, or that never stores: a retrieval, or a store
        /// that is skipped. Its stores carry neither a namespace nor a target.
        /// </summary>
        /// <param name="converter">Converter whose storage configuration to offload through.</param>
        /// <param name="skipStore">
        /// When true, offload is skipped and payloads are left inline; retrieval is unaffected.
        /// </param>
        /// <returns>A context, never null.</returns>
        internal static ExternalStorageContext For(DataConverter converter, bool skipStore = false) =>
            new(
                ExternalStorageRunner.Create(converter.ExternalStorage, @namespace: null),
                target: null,
                skipStore);

        /// <summary>
        /// Create a context over the given converter's storage configuration for a store performed
        /// by a client or worker.
        /// </summary>
        /// <param name="converter">Converter whose storage configuration to offload through.</param>
        /// <param name="namespace">Namespace of the client or worker performing the store.</param>
        /// <param name="target">
        /// Execution the stored payloads are offloaded on behalf of, or null when none is known.
        /// </param>
        /// <returns>A context, never null.</returns>
        internal static ExternalStorageContext For(
            DataConverter converter, string @namespace, IStorageDriverTargetInfo? target) =>
            new(ExternalStorageRunner.Create(converter.ExternalStorage, @namespace), target);
    }
}
