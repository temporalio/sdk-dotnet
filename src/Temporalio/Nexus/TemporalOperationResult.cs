using System;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Unified result type for Temporal-backed Nexus operations. Encapsulates either a synchronous
    /// result value or an asynchronous operation token.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <remarks>
    /// <para>WARNING: Nexus support is experimental.</para>
    /// <para>Use <see cref="SyncResult"/> for operations that complete immediately (e.g., signals).
    /// Use <see cref="AsyncResult"/> for operations that return an operation token for async
    /// completion (e.g., starting a workflow).</para>
    /// </remarks>
    public sealed class TemporalOperationResult<TResult>
    {
        private readonly TResult? syncValue;
        private readonly string? asyncToken;

        private TemporalOperationResult(bool isSyncResult, TResult? syncValue, string? asyncToken)
        {
            IsSyncResult = isSyncResult;
            this.syncValue = syncValue;
            this.asyncToken = asyncToken;
        }

        /// <summary>
        /// Gets a value indicating whether this is a synchronous result.
        /// </summary>
        public bool IsSyncResult { get; }

        /// <summary>
        /// Gets the synchronous result value. Only valid when <see cref="IsSyncResult"/> is true.
        /// </summary>
        /// <exception cref="InvalidOperationException">If accessed on an async result.</exception>
        internal TResult? SyncValue => IsSyncResult
            ? syncValue
            : throw new InvalidOperationException("Not a sync result");

        /// <summary>
        /// Gets the asynchronous operation token. Only valid when <see cref="IsSyncResult"/> is
        /// false.
        /// </summary>
        /// <exception cref="InvalidOperationException">If accessed on a sync result.</exception>
        internal string AsyncToken => IsSyncResult
            ? throw new InvalidOperationException("Not an async result")
            : asyncToken!;

        /// <summary>
        /// Create a synchronous result with the given value.
        /// </summary>
        /// <param name="value">The result value.</param>
        /// <returns>A synchronous operation result.</returns>
#pragma warning disable CA1000 // Intentional static factory on generic type
        public static TemporalOperationResult<TResult> SyncResult(TResult? value) =>
            new(isSyncResult: true, syncValue: value, asyncToken: null);
#pragma warning restore CA1000

        /// <summary>
        /// Create an asynchronous result with the given operation token.
        /// </summary>
        /// <param name="token">The operation token.</param>
        /// <returns>An asynchronous operation result.</returns>
        /// <exception cref="ArgumentException">If the token is null or empty.</exception>
#pragma warning disable CA1000 // Intentional static factory on generic type
        public static TemporalOperationResult<TResult> AsyncResult(string token)
#pragma warning restore CA1000
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token cannot be null or empty", nameof(token));
            }
            return new(isSyncResult: false, syncValue: default, asyncToken: token);
        }
    }
}
