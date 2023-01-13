using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception that is thrown by the server to workflows when an activity fails.
    /// </summary>
    public class ActivityFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        internal protected ActivityFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.ActivityFailureInfo == null)
            {
                throw new ArgumentException("Missing activity failure info");
            }
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the identity of the worker where this failed.
        /// </summary>
        public string? Identity
        {
            get
            {
                var identity = Failure!.ActivityFailureInfo.Identity;
                return identity.Length == 0 ? null : identity;
            }
        }

        /// <summary>
        /// Gets the activity name or "type" that failed.
        /// </summary>
        public string ActivityType => Failure!.ActivityFailureInfo.ActivityType.Name;

        /// <summary>
        /// Gets the identifier of the activity that failed.
        /// </summary>
        public string ActivityID => Failure!.ActivityFailureInfo.ActivityId;

        /// <summary>
        /// Gets the retry state for the failure.
        /// </summary>
        public RetryState RetryState => Failure!.ActivityFailureInfo.RetryState;
    }
}
