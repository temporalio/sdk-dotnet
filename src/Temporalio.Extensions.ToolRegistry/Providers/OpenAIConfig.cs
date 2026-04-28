#if NET8_0_OR_GREATER
using System;
using OpenAI.Chat;

namespace Temporalio.Extensions.ToolRegistry.Providers
{
    /// <summary>
    /// Configuration for <see cref="OpenAIProvider"/>.
    /// </summary>
    public sealed class OpenAIConfig
    {
        /// <summary>
        /// Gets or sets the OpenAI API key. Required unless <see cref="Client"/> is set.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the model name. Defaults to <c>gpt-4o</c>.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the base URL override (e.g. for proxies or test servers).
        /// When set, overrides the default OpenAI API endpoint.
        /// </summary>
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets a pre-constructed <see cref="ChatClient"/>. When set,
        /// <see cref="ApiKey"/> and <see cref="BaseUrl"/> are ignored.
        /// </summary>
        public ChatClient? Client { get; set; }
    }
}
#endif
