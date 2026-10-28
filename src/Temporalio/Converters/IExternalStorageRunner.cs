using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Worker;

namespace Temporalio.Converters
{
    /// <summary>
    /// Retrieves external storage references from received payloads.
    /// </summary>
    internal interface IExternalStorageRunner
    {
        /// <summary>
        /// Retrieve the references in a message by visiting its payloads.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="visit">
        /// The generated <c>PayloadVisitors.VisitAsync</c> overload for <typeparamref name="T"/>,
        /// passed as a method group.
        /// </param>
        /// <param name="message">The message to visit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the message has been visited.</returns>
        Task RetrieveAsync<T>(
            Func<T, PayloadVisitors.PayloadVisitor, PayloadVisitors.PayloadsVisitor, Task> visit,
            T message,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve the references in the given payloads.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resulting payloads.</returns>
        Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            IReadOnlyCollection<Payload> payloads, CancellationToken cancellationToken);
    }
}
