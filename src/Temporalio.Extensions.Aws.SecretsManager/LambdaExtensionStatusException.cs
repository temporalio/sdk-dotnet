using System;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A non-200 response from the Lambda extension.
    /// </summary>
#pragma warning disable CA1064 // Internal, users only see it as an Exception
    internal sealed class LambdaExtensionStatusException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaExtensionStatusException"/> class.
        /// </summary>
        /// <param name="status">HTTP status.</param>
        /// <param name="body">Response body.</param>
        public LambdaExtensionStatusException(int status, string body)
            : base($"AWS Parameters and Secrets Lambda Extension returned status {status}: {body}") =>
            Status = status;

        /// <summary>
        /// Gets the HTTP status.
        /// </summary>
        public int Status { get; }
    }
#pragma warning restore CA1064
}
