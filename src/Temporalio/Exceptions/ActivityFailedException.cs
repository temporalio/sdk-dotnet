using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a standalone activity has failed while waiting for the result.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityFailedException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityFailedException"/> class.
        /// </summary>
        /// <param name="inner">Cause of the exception.</param>
        public ActivityFailedException(Exception? inner)
            : base("Activity failed", inner)
        {
        }
    }
}
