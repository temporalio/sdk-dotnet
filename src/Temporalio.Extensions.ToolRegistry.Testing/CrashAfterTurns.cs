using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// Implements <see cref="IProvider"/> and returns an error after <see cref="N"/> complete
    /// turns. Use it in integration tests to verify that <see cref="AgenticSession"/> resumes from
    /// a heartbeat checkpoint after a simulated crash.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// // First invocation returns an error after 2 turns.
    /// // Second invocation (retry) resumes from the last checkpoint.
    /// var provider = new CrashAfterTurns { N = 2 };
    /// </code>
    /// </remarks>
    public sealed class CrashAfterTurns : IProvider
    {
        private int count;

        /// <summary>
        /// Gets or sets the number of turns to complete before throwing.
        /// </summary>
        public int N { get; set; }

        /// <summary>
        /// Gets or sets an optional delegate provider to forward turns to for the first
        /// <see cref="N"/> turns. When <c>null</c>, a stub assistant response is returned
        /// instead.
        /// </summary>
        public IProvider? Delegate { get; set; }

        /// <summary>
        /// Completes a turn normally for the first <see cref="N"/> turns, then throws.
        /// </summary>
        /// <inheritdoc/>
        public Task<ProviderTurnResult> RunTurnAsync(
            IList<Dictionary<string, object?>> messages,
            IReadOnlyList<ToolDef> tools,
            CancellationToken cancellationToken = default)
        {
            count++;
            if (count > N)
            {
                throw new InvalidOperationException(
                    $"CrashAfterTurns: simulated crash after {N} turns");
            }

            if (Delegate != null)
            {
                return Delegate.RunTurnAsync(messages, tools, cancellationToken);
            }

            var newMessages = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["role"] = "assistant",
                    ["content"] = new List<object?>
                    {
                        new Dictionary<string, object?> { ["type"] = "text", ["text"] = "..." },
                    },
                },
            };
            return Task.FromResult(new ProviderTurnResult(newMessages, Done: count >= N));
        }
    }
}
