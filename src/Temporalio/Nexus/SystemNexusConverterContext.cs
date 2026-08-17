using System;
using System.Threading;
using Temporalio.Converters;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Provides the user configured converters while System Nexus transfer conversion runs.
    /// </summary>
    /// <remarks>
    /// This context is only available while a System Nexus workflow operation converts its input
    /// or result through a Temporal transfer type converter.
    /// </remarks>
    public static class SystemNexusConverterContext
    {
        private static readonly AsyncLocal<ConverterContext?> CurrentLocal = new();

        /// <summary>
        /// Gets the application's payload converter for the current System Nexus conversion.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called outside a System Nexus transfer conversion.
        /// </exception>
        public static IPayloadConverter PayloadConverter => Current.PayloadConverter;

        /// <summary>
        /// Gets the application's failure converter for the current System Nexus conversion.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called outside a System Nexus transfer conversion.
        /// </exception>
        public static IFailureConverter FailureConverter => Current.FailureConverter;

        private static ConverterContext Current => CurrentLocal.Value ?? throw new InvalidOperationException(
            "The System Nexus converter context is only available while a System Nexus transfer type converter is executing.");

        /// <summary>
        /// Sets the converters for the duration of the returned scope.
        /// </summary>
        /// <param name="payloadConverter">The application's payload converter.</param>
        /// <param name="failureConverter">The application's failure converter.</param>
        /// <returns>A scope that restores the preceding converter context.</returns>
        internal static IDisposable Push(
            IPayloadConverter payloadConverter,
            IFailureConverter failureConverter)
        {
            var previous = CurrentLocal.Value;
            CurrentLocal.Value = new(payloadConverter, failureConverter);
            return new PopOnDispose(previous);
        }

        private sealed record ConverterContext(
            IPayloadConverter PayloadConverter,
            IFailureConverter FailureConverter);

        private sealed class PopOnDispose : IDisposable
        {
            private readonly ConverterContext? previous;
            private bool disposed;

            internal PopOnDispose(ConverterContext? previous) => this.previous = previous;

            public void Dispose()
            {
                if (!disposed)
                {
                    CurrentLocal.Value = previous;
                    disposed = true;
                }
            }
        }
    }
}
