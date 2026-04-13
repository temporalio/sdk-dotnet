using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// Maintains conversation state (messages and issues) across multiple turns of a tool-calling
    /// loop, with heartbeat checkpointing for crash recovery.
    /// </summary>
    /// <remarks>
    /// Use <see cref="RunWithSessionAsync(Func{AgenticSession, Task}, CancellationToken)"/> inside a
    /// Temporal activity to get automatic checkpoint restore-on-retry and heartbeat on each turn.
    /// <code>
    /// await AgenticSession.RunWithSessionAsync(async session =>
    /// {
    ///     await session.RunToolLoopAsync(provider, registry, system, prompt);
    /// });
    /// </code>
    /// </remarks>
    public sealed class AgenticSession
    {
        private readonly List<Dictionary<string, object?>> messages = new();
        private readonly List<Dictionary<string, object?>> issues = new();

        /// <summary>
        /// Gets the full conversation history. Append-only during a session.
        /// </summary>
        public IList<Dictionary<string, object?>> Messages => messages;

        /// <summary>
        /// Gets the accumulated application-level results from tool calls. Elements must be
        /// JSON-serializable for checkpoint storage.
        /// </summary>
        public IList<Dictionary<string, object?>> Issues => issues;

        /// <summary>
        /// Runs <paramref name="fn"/> inside an <see cref="AgenticSession"/>, restoring from a
        /// heartbeat checkpoint if one exists (i.e., on activity retry after crash).
        /// </summary>
        /// <remarks>
        /// Must be called from within a Temporal activity.
        /// </remarks>
        /// <param name="fn">The async function to run with the session.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task RunWithSessionAsync(
            Func<AgenticSession, Task> fn,
            CancellationToken cancellationToken = default) =>
            RunWithSessionAsync<object?>(
                async session =>
                {
                    await fn(session).ConfigureAwait(false);
                    return null;
                },
                cancellationToken);

        /// <summary>
        /// Runs <paramref name="fn"/> inside an <see cref="AgenticSession"/>, restoring from a
        /// heartbeat checkpoint if one exists (i.e., on activity retry after crash).
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <remarks>
        /// Must be called from within a Temporal activity.
        /// </remarks>
        /// <param name="fn">The async function to run with the session.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Value returned by <paramref name="fn"/>.</returns>
        public static async Task<T> RunWithSessionAsync<T>(
            Func<AgenticSession, Task<T>> fn,
            CancellationToken cancellationToken = default)
        {
            // Access current context before the try-catch so InvalidOperationException
            // propagates directly when called outside a Temporal activity.
            var activityContext = ActivityExecutionContext.Current;
            var session = new AgenticSession();
            if (activityContext.Info.HeartbeatDetails.Count > 0)
            {
                try
                {
                    var cp = await activityContext.Info
                        .HeartbeatDetailAtAsync<SessionCheckpoint>(0).ConfigureAwait(false);
                    bool shouldRestore = true;
                    if (cp?.Version == 0)
                    {
                        activityContext.Logger.LogWarning(
                            "AgenticSession: checkpoint has no version field" +
                            " — may be from an older release");
                    }
                    else if (cp?.Version != 1)
                    {
                        activityContext.Logger.LogWarning(
                            "AgenticSession: checkpoint version {Version}, expected 1 — starting fresh",
                            cp?.Version);
                        shouldRestore = false;
                    }

                    if (shouldRestore)
                    {
                        if (cp?.Messages?.Count > 0)
                        {
                            session.messages.AddRange(JsonElementConverter.MaterializeList(cp.Messages));
                        }

                        if (cp?.Issues?.Count > 0)
                        {
                            session.issues.AddRange(JsonElementConverter.MaterializeList(cp.Issues));
                        }
                    }
                }
#pragma warning disable CA1031 // corrupt checkpoint — warn and start fresh
                catch (Exception e)
                {
                    activityContext.Logger.LogWarning(
                        "AgenticSession: failed to decode checkpoint, starting fresh: {Error}",
                        e.Message);
                }
#pragma warning restore CA1031
            }

            return await fn(session).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs the multi-turn LLM tool-calling loop, heartbeating before each turn.
        /// </summary>
        /// <remarks>
        /// If <see cref="Messages"/> is empty (fresh start), <paramref name="prompt"/> is added as
        /// the first user message. Otherwise the existing conversation state is resumed (retry case).
        /// <para>
        /// On every turn it checkpoints via <see cref="Checkpoint"/> before calling the LLM, then
        /// checks the cancellation token. If the activity is cancelled, the loop returns
        /// immediately. The next attempt will restore from the last checkpoint.
        /// </para>
        /// </remarks>
        /// <param name="provider">LLM provider adapter.</param>
        /// <param name="registry">Tool registry.</param>
        /// <param name="system">System prompt (passed to provider at construction time).</param>
        /// <param name="prompt">Initial user prompt.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RunToolLoopAsync(
            IProvider provider,
            ToolRegistry registry,
            string system,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            if (messages.Count == 0)
            {
                messages.Add(new() { ["role"] = "user", ["content"] = prompt });
            }

            while (true)
            {
                Checkpoint(cancellationToken);

                var result = await provider.RunTurnAsync(
                    messages, registry.Definitions(), cancellationToken).ConfigureAwait(false);

                foreach (var msg in result.NewMessages)
                {
                    messages.Add(msg);
                }

                if (result.Done)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Heartbeats the current session state to Temporal, then checks the cancellation token.
        /// </summary>
        /// <remarks>
        /// Called automatically by <see cref="RunToolLoopAsync"/> before each turn, but can also be
        /// called manually between tool dispatches.
        /// </remarks>
        /// <param name="cancellationToken">Cancellation token to check after heartbeating.</param>
        /// <exception cref="OperationCanceledException">
        /// If <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        public void Checkpoint(CancellationToken cancellationToken = default)
        {
            // T10: validate all issues are JSON-serializable before heartbeating.
            for (int i = 0; i < issues.Count; i++)
            {
                try
                {
                    JsonSerializer.Serialize(issues[i]);
                }
                catch (JsonException e)
                {
                    throw new ApplicationFailureException(
                        $"AgenticSession: issues[{i}] is not JSON-serializable: {e.Message}. " +
                        "Store only Dictionary<string, object?> with JSON-serializable values.",
                        nonRetryable: true);
                }
            }

            var cp = new SessionCheckpoint
            {
                Messages = new(messages),
                Issues = new(issues),
            };
            ActivityExecutionContext.Current.Heartbeat(cp);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
