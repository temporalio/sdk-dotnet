using System;
using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// A scripted provider response produced by <see cref="Done(string)"/> or
    /// <see cref="ToolCall(string, IReadOnlyDictionary{string, object?}, string?)"/>.
    /// </summary>
    public sealed class MockResponse
    {
        private MockResponse(bool stop, IReadOnlyList<Dictionary<string, object?>> content)
        {
            Stop = stop;
            Content = content;
        }

        /// <summary>
        /// Gets a value indicating whether this response ends the tool loop.
        /// </summary>
        internal bool Stop { get; }

        /// <summary>
        /// Gets the list of content blocks in this response.
        /// </summary>
        internal IReadOnlyList<Dictionary<string, object?>> Content { get; }

        /// <summary>
        /// Returns a <see cref="MockResponse"/> that ends the loop with the given text.
        /// </summary>
        /// <param name="text">Text to include in the assistant response. Defaults to "Done.".</param>
        /// <returns>A <see cref="MockResponse"/> with <see cref="Stop"/> set to <c>true</c>.</returns>
        public static MockResponse Done(string text = "Done.") =>
            new(
                stop: true,
                content: new[] { new Dictionary<string, object?> { ["type"] = "text", ["text"] = text } });

        /// <summary>
        /// Returns a <see cref="MockResponse"/> that makes a single tool call.
        /// </summary>
        /// <param name="toolName">Name of the tool to call.</param>
        /// <param name="toolInput">Input to pass to the tool.</param>
        /// <param name="callId">
        /// Optional tool call ID. A random ID is generated if null or empty.
        /// </param>
        /// <returns>A <see cref="MockResponse"/> with <see cref="Stop"/> set to <c>false</c>.</returns>
        public static MockResponse ToolCall(
            string toolName,
            IReadOnlyDictionary<string, object?> toolInput,
            string? callId = null)
        {
            var id = string.IsNullOrEmpty(callId)
                ? $"test_{Guid.NewGuid():N}".Substring(0, 16)
                : callId;
            return new(
                stop: false,
                content: new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "tool_use",
                        ["id"] = id,
                        ["name"] = toolName,
                        ["input"] = toolInput,
                    },
                });
        }
    }
}
