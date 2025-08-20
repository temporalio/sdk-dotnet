using System;

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
    [Obsolete("Worker Build Id versioning is deprecated in favor of Worker Deployment versioning")]
    public enum VersioningIntent
    {
        /// <summary>
        /// Indicates that core should choose the most sensible default behavior for the type of
        /// command, accounting for whether the command will be run on the same task queue as the
        /// current worker.
        /// </summary>
        Unspecified = (int)Bridge.Api.Common.VersioningIntent.Unspecified,

        /// <summary>
        /// Indicates that the command should run on a worker with compatible version if
        /// possible. It may not be possible if the target task queue does not also have knowledge of the
        /// current worker's Build Id.
        /// </summary>
        Compatible = (int)Bridge.Api.Common.VersioningIntent.Compatible,

        /// <summary>
        /// Indicates that the command should run on the target task queue's current overall-default Build Id.
        /// </summary>
        CurrentDefault = (int)Bridge.Api.Common.VersioningIntent.Default,
    }
}
