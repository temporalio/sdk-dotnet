using Temporalio.Nexus;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for a standalone Nexus operation with typed completion callbacks.
    /// </summary>
    /// <typeparam name="TOutput">The type of the operation's result.</typeparam>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class NexusOperationOptions<TOutput> : NexusOperationOptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions{TOutput}"/> class.
        /// </summary>
        public NexusOperationOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions{TOutput}"/> class.
        /// </summary>
        /// <param name="id">The operation ID.</param>
        public NexusOperationOptions(string id) => Id = id;

        /// <summary>
        /// Gets or sets callbacks that accept the operation's result or raw payloads.
        /// </summary>
        public CompletionCallbackCollection<TOutput>? CompletionCallbacks { get; set; }

        /// <summary>
        /// Converts typed options to the client's request options.
        /// </summary>
        /// <returns>The request options with the callback descriptions.</returns>
        internal NexusOperationOptions ToUntyped() =>
            new(this) { CompletionCallbacks = CompletionCallbacks };
    }
}
