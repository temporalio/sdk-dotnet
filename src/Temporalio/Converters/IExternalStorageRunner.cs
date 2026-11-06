using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Worker;

namespace Temporalio.Converters
{
    /// <summary>
    /// Offloads large payloads to external storage and retrieves references back.
    /// </summary>
    internal interface IExternalStorageRunner
    {
        /// <summary>
        /// Store the payloads in a message by visiting its payloads.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="walk">
        /// The generated <c>PayloadVisitors.Walk</c> overload for <typeparamref name="T"/>, passed
        /// as a method group.
        /// </param>
        /// <param name="message">The message to visit.</param>
        /// <param name="target">
        /// Execution the stored payloads are offloaded on behalf of, or null when none is known.
        /// </param>
        /// <param name="metrics">
        /// Accumulates what this pass stored, or null to not measure it.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the message has been visited.</returns>
        Task StoreAsync<T>(
            Action<T, PayloadVisitors.WalkEnv, PayloadVisitors.VisitState> walk,
            T message,
            IStorageDriverTargetInfo? target,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken);

        /// <summary>
        /// Store the given payloads.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="target">
        /// Execution the stored payloads are offloaded on behalf of, or null when none is known.
        /// </param>
        /// <param name="metrics">
        /// Accumulates what was stored, or null to not measure it.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resulting payloads.</returns>
        Task<IReadOnlyCollection<Payload>> StoreAsync(
            IReadOnlyCollection<Payload> payloads,
            IStorageDriverTargetInfo? target,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve the references in a message by visiting its payloads.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="walk">
        /// The generated <c>PayloadVisitors.Walk</c> overload for <typeparamref name="T"/>, passed
        /// as a method group.
        /// </param>
        /// <param name="message">The message to visit.</param>
        /// <param name="metrics">
        /// Accumulates what this pass retrieved, or null to not measure it.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the message has been visited.</returns>
        Task RetrieveAsync<T>(
            Action<T, PayloadVisitors.WalkEnv, PayloadVisitors.VisitState> walk,
            T message,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve the references in the given payloads.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="metrics">
        /// Accumulates what was retrieved, or null to not measure it.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resulting payloads.</returns>
        Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            IReadOnlyCollection<Payload> payloads,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken);
    }
}
