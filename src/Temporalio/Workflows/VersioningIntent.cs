namespace Temporalio.Workflows
{
    /// <summary>
    /// Indicates whether the user intends certain commands to be run on a compatible worker Build
    /// Id version or not.
    ///
    /// Where this type is accepted optionally, an unset value indicates that the SDK should choose the
    /// most sensible default behavior for the type of command, accounting for whether the command will
    /// be run on the same task queue as the current worker.
    /// </summary>
    public abstract record VersioningIntent
    {
        private VersioningIntent()
        {
        }

        /// <summary>
        /// Convert this versioning intent to its protobuf equivalent.
        /// </summary>
        /// <returns>Protobuf.</returns>
        internal abstract Temporalio.Bridge.Api.Common.VersioningIntent ToProto();

        /// <summary>
        /// Indicates that the command should run on a worker with compatible version if
        /// possible. It may not be possible if the target task queue does not also have knowledge of the
        /// current worker's Build Id.
        /// </summary>
        public record Compatible : VersioningIntent
        {
            /// <inheritdoc/>
            internal override Bridge.Api.Common.VersioningIntent ToProto() =>
                Bridge.Api.Common.VersioningIntent.Compatible;
        }

        /// <summary>
        /// Indicates that the command should run on the target task queue's current overall-default Build Id.
        /// </summary>
        public record CurrentDefault : VersioningIntent
        {
            /// <inheritdoc/>
            internal override Bridge.Api.Common.VersioningIntent ToProto() =>
                Bridge.Api.Common.VersioningIntent.Default;
        }
    }
}