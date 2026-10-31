using System.Linq;

namespace Temporalio.Converters
{
    /// <summary>
    /// Internal helpers over <see cref="ExternalStorage" />.
    /// </summary>
    internal static class ExternalStorageExtensions
    {
        /// <summary>
        /// Get the driver registered under the given name.
        /// </summary>
        /// <param name="storage">Storage whose drivers to search.</param>
        /// <param name="name">Driver name recorded in the payload reference.</param>
        /// <returns>The driver, or null if no driver is registered under that name.</returns>
        internal static IStorageDriver? GetDriver(this ExternalStorage storage, string name) =>
            storage.Drivers.FirstOrDefault(driver => driver.Name == name);

        /// <summary>
        /// Get the distinct driver types, for reporting in worker heartbeats.
        /// </summary>
        /// <param name="storage">Storage whose drivers to report.</param>
        /// <returns>The distinct <see cref="IStorageDriver.Type" /> values across all drivers.</returns>
        internal static string[] GetDistinctDriverTypes(this ExternalStorage storage) =>
            storage.Drivers.Select(driver => driver.Type).Distinct().ToArray();
    }
}
