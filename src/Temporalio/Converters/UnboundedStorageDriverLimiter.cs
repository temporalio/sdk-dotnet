using System;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Converters
{
    /// <summary>
    /// Limiter that runs every request immediately without bounding concurrency. Used as the
    /// default until a real concurrency limit is configured.
    /// </summary>
    /// <typeparam name="TItem">What a single request covers.</typeparam>
    internal sealed class UnboundedStorageDriverLimiter<TItem> : IStorageDriverLimiter<TItem>
    {
        /// <summary>
        /// Shared instance. The limiter holds no state, so one instance serves every caller.
        /// </summary>
        public static readonly UnboundedStorageDriverLimiter<TItem> Instance = new();

        /// <inheritdoc />
        public Task<TResult> RunAsync<TResult>(
            TItem item, Func<Task<TResult>> operation, CancellationToken cancellationToken) =>
            operation();
    }
}
