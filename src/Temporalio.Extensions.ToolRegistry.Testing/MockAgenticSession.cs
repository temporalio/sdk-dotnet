using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// A pre-canned session that returns fixed issues without any LLM calls.
    /// Use it to test code that calls <see cref="AgenticSession.RunWithSessionAsync"/> and
    /// inspects session state without an API key or a Temporal server.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// var session = new MockAgenticSession
    /// {
    ///     Issues = { new Dictionary&lt;string, object?&gt; { ["type"] = "missing", ["symbol"] = "x" } },
    /// };
    /// await session.RunToolLoopAsync(null!, null!, "sys", "prompt");
    /// // session.Issues still contains the pre-canned entry
    /// </code>
    /// </remarks>
    public sealed class MockAgenticSession
    {
        private readonly List<Dictionary<string, object?>> messages = new();
        private readonly List<Dictionary<string, object?>> issues = new();

        /// <summary>
        /// Gets the pre-canned or accumulated conversation messages.
        /// </summary>
        public IList<Dictionary<string, object?>> Messages => messages;

        /// <summary>
        /// Gets the pre-canned or accumulated issues.
        /// </summary>
        public IList<Dictionary<string, object?>> Issues => issues;

        /// <summary>
        /// Gets the prompt value that was passed to the last call of
        /// <see cref="RunToolLoopAsync"/>. Useful for asserting that callers pass the correct
        /// initial prompt.
        /// </summary>
        public string? CapturedPrompt { get; private set; }

        /// <summary>
        /// No-op: does not call any LLM or record a heartbeat. Adds the prompt as the first user
        /// message if <see cref="Messages"/> is empty.
        /// </summary>
        /// <param name="provider">Not used; accepted for interface compatibility.</param>
        /// <param name="registry">Not used; may be null.</param>
        /// <param name="system">Not used; present for API symmetry.</param>
        /// <param name="prompt">Initial user prompt (added if messages are empty).</param>
        /// <param name="cancellationToken">Not used.</param>
        /// <returns>A completed task.</returns>
        public Task RunToolLoopAsync(
            IProvider? provider,
            ToolRegistry? registry,
            string system,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            CapturedPrompt = prompt;
            if (messages.Count == 0)
            {
                messages.Add(new() { ["role"] = "user", ["content"] = prompt });
            }
            return Task.CompletedTask;
        }
    }
}
