using System;
using Temporalio.Exceptions;

namespace Temporalio.Converters
{
    /// <summary>
    /// Creates application failures that report invalid Nexus operation input from payload
    /// conversion.
    /// </summary>
    public static class PayloadValidationError
    {
        /// <summary>
        /// Application failure type reserved for payload validation errors.
        /// </summary>
        internal const string ErrorType = "PayloadValidationError";

        /// <summary>
        /// Creates a non-retryable application failure for invalid Nexus operation input.
        /// </summary>
        /// <param name="details">
        /// Validation error details to serialize with the failure, or null to omit failure details.
        /// </param>
        /// <returns>The payload validation application failure.</returns>
        public static ApplicationFailureException CreateException(object? details) =>
            new(
                "Payload validation failed",
                errorType: ErrorType,
                nonRetryable: true,
                details: details == null ? Array.Empty<object?>() : new object?[] { details });

        /// <summary>
        /// Whether the given exception is a payload validation application failure.
        /// </summary>
        /// <param name="exception">Exception to check.</param>
        /// <returns>True if the exception reports an invalid payload.</returns>
        internal static bool IsException(Exception exception) =>
            exception is ApplicationFailureException appException &&
            appException.NonRetryable &&
            appException.ErrorType == ErrorType;
    }
}
