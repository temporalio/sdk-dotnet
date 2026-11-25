using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Plugin name and version reported in worker heartbeats.
    /// </summary>
    /// <param name="Name">Plugin name.</param>
    /// <param name="Version">Plugin version, or empty if the plugin does not report one.</param>
    internal record PluginInfo(string Name, string Version)
    {
        /// <summary>
        /// Create plugin info for a client plugin.
        /// </summary>
        /// <param name="plugin">Client plugin.</param>
        /// <returns>Plugin info.</returns>
        public static PluginInfo FromPlugin(ITemporalClientPlugin plugin) =>
            new(plugin.Name, (plugin as ITemporalVersionedPlugin)?.Version ?? string.Empty);

        /// <summary>
        /// Create plugin info for a worker plugin.
        /// </summary>
        /// <param name="plugin">Worker plugin.</param>
        /// <returns>Plugin info.</returns>
        public static PluginInfo FromPlugin(ITemporalWorkerPlugin plugin) =>
            new(plugin.Name, (plugin as ITemporalVersionedPlugin)?.Version ?? string.Empty);
    }
}
