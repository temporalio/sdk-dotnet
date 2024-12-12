using System;
using System.Threading;

namespace Temporalio.Activities
{
    /// <summary>
    /// Provides access to the scoped <see cref="IServiceProvider" /> for the
    /// current activity execution context.
    /// </summary>
    public static class ActivityServiceProviderAccessor
    {
        /// <summary>
        /// Gets the async local current value.
        /// </summary>
        public static readonly AsyncLocal<IServiceProvider?> AsyncLocalCurrent = new();

        /// <summary>
        /// Gets a value indicating whether the current code is running in an
        /// activity with a service provider.
        /// </summary>
        public static bool HasCurrent => AsyncLocalCurrent.Value != null;

        /// <summary>
        /// Gets the current activity's scoped <see cref="IServiceProvider"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">If no <see
        /// cref="IServiceProvider"/> is available.</exception>
        public static IServiceProvider Current => AsyncLocalCurrent.Value ??
            throw new InvalidOperationException("No current service provider");
    }
}
