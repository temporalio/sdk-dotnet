#if NET8_0_OR_GREATER
using System;
using Anthropic;

namespace Temporalio.Extensions.ToolRegistry.Providers
{
    /// <summary>
    /// Configuration for <see cref="AnthropicProvider"/>.
    /// </summary>
    public sealed class AnthropicConfig
    {
        /// <summary>
        /// Gets or sets the Anthropic API key. Required unless <see cref="Client"/> is set.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the model name. Defaults to <c>claude-sonnet-4-6</c>.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the base URL override (e.g. for proxies or test servers).
        /// When set, overrides the default Anthropic API endpoint.
        /// </summary>
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets a pre-constructed Anthropic client. When set, <see cref="ApiKey"/> and
        /// <see cref="BaseUrl"/> are ignored.
        /// </summary>
        public AnthropicClient? Client { get; set; }
    }
}
#endif
