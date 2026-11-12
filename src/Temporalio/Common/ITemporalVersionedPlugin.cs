namespace Temporalio.Common
{
    /// <summary>
    /// Interface for a plugin to report its version.
    /// </summary>
    /// <remarks>
    /// This is only used on an implementation of <see cref="Client.ITemporalClientPlugin"/> or
    /// <see cref="Worker.ITemporalWorkerPlugin"/>. The SDK reports the version alongside the
    /// plugin name, such as in worker heartbeats.
    /// </remarks>
    public interface ITemporalVersionedPlugin
    {
        /// <summary>
        /// Gets the plugin version, or an empty string if the plugin has no version.
        /// </summary>
        string Version { get; }
    }
}
