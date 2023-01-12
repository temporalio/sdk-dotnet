using System;

using Temporalio.Api.Failure.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Representation of a failure converter to convert exceptions to/from Temporal failures.
    /// </summary>
    /// <remarks>
    /// This converter should be deterministic since it is used for workflows. For the same reason,
    /// this converter should be immediate and avoid any network calls or any asynchronous/slow code
    /// paths.
    /// </remarks>
    /// <seealso cref="DefaultFailureConverter" />
    public interface IFailureConverter
    {
        /// <summary>
        /// Convert the given exception to a failure.
        /// </summary>
        /// <param name="exception">Exception to convert.</param>
        /// <param name="payloadConverter">Payload converter to use if needed.</param>
        /// <returns>Created failure.</returns>
        Failure ToFailure(Exception exception, IPayloadConverter payloadConverter);

        /// <summary>
        /// Convert the given failure to an exception.
        /// </summary>
        /// <param name="failure">Failure to convert.</param>
        /// <param name="payloadConverter">Payload converter to use if needed.</param>
        /// <returns>Created exception.</returns>
        Exception ToException(Failure failure, IPayloadConverter payloadConverter);
    }
}
