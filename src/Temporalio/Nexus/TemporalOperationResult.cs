using System;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Unified result type for Temporal-backed Nexus operations. Encapsulates either a synchronous
    /// result value or an asynchronous operation token.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public sealed class TemporalOperationResult<TResult>
    {
        private TemporalOperationResult(bool isSyncResult, TResult? syncValue, string? asyncToken)
        {
            IsSyncResult = isSyncResult;
            SyncValue = syncValue;
            AsyncToken = asyncToken;
        }

        /// <summary>
        /// Gets a value indicating whether this is a synchronous result.
        /// </summary>
        public bool IsSyncResult { get; }

        /// <summary>
        /// Gets the synchronous result value. Only meaningful when <see cref="IsSyncResult"/> is
        /// true.
        /// </summary>
        public TResult? SyncValue { get; }

        /// <summary>
        /// Gets the asynchronous operation token. Only meaningful when <see cref="IsSyncResult"/>
        /// is false.
        /// </summary>
        public string? AsyncToken { get; }

        /// <summary>
        /// Create a synchronous result with the given value.
        /// </summary>
        /// <param name="value">The result value.</param>
        /// <returns>A synchronous operation result.</returns>
#pragma warning disable CA1000 // Intentional static factory on generic type
        public static TemporalOperationResult<TResult> Sync(TResult? value) =>
            new(isSyncResult: true, syncValue: value, asyncToken: null);
#pragma warning restore CA1000

        /// <summary>
        /// Create an asynchronous result with the given operation token.
        /// </summary>
        /// <param name="token">The operation token.</param>
        /// <returns>An asynchronous operation result.</returns>
        /// <exception cref="ArgumentException">If the token is null or empty.</exception>
#pragma warning disable CA1000, VSTHRD200 // Intentional: static factory on generic type; "Async" refers to the result kind, not the method signature
        internal static TemporalOperationResult<TResult> Async(string token)
#pragma warning restore CA1000, VSTHRD200
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token cannot be null or empty", nameof(token));
            }
            return new(isSyncResult: false, syncValue: default, asyncToken: token);
        }
    }
}
