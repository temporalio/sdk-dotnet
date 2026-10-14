#pragma warning disable SA1600 // Internal implementation plumbing.

using System;
using System.Threading;
using Temporalio.Converters;

namespace Temporalio.Nexus.SystemNexus
{
    internal static class SystemNexusConverterContext
    {
        private static readonly AsyncLocal<ConverterContext?> CurrentLocal = new();

        internal static IPayloadConverter PayloadConverter => Current.PayloadConverter;

        internal static IFailureConverter FailureConverter => Current.FailureConverter;

        private static ConverterContext Current => CurrentLocal.Value ?? throw new InvalidOperationException(
            "The System Nexus converter context is only available while a System Nexus transfer type converter is executing.");

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
