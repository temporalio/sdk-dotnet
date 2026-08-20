#pragma warning disable CA1711 // Factory naming matches the payload validation API across SDKs

using System;
using Temporalio.Exceptions;

namespace Temporalio.Converters
{
    /// <summary>
    /// Factory for application failures that report invalid Nexus operation input from payload
    /// conversion.
    /// </summary>
    public static class PayloadValidationException
    {
        /// <summary>
        /// Application failure type reserved for payload validation errors.
        /// </summary>
        internal const string ErrorType = "PayloadValidationError";

        /// <summary>
        /// Creates a non-retryable application failure for invalid Nexus operation input.
        /// </summary>
        /// <param name="details">Validation error details to serialize with the failure.</param>
        /// <returns>The payload validation application failure.</returns>
        public static ApplicationFailureException Create(object? details) =>
            new(
                "Payload validation failed",
                errorType: ErrorType,
                nonRetryable: true,
                details: details == null ? Array.Empty<object?>() : new object?[] { details });
    }
}
