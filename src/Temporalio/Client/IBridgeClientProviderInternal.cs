namespace Temporalio.Client
{
    /// <summary>
    /// Internal interface that provides access to bridge client and runtime.
    /// </summary>
    internal interface IBridgeClientProviderInternal : IBridgeClientProvider
    {
        /// <summary>
        /// Gets the safe client handle for internal bridge use.
        /// </summary>
        Bridge.SafeClientHandle? ClientHandle { get; }

        /// <summary>
        /// Gets the runtime associated with the client.
        /// </summary>
        Bridge.Runtime? Runtime { get; }
    }
}
