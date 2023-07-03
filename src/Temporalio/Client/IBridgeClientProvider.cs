using System.Runtime.InteropServices;

namespace Temporalio.Client
{
    /// <summary>
    /// Provides a handle to the underlying bridge for use internally.
    /// </summary>
    /// <remarks>
    /// Developers should not implement this directly. It is already implemented by
    /// <see cref="ITemporalConnection" />.
    /// </remarks>
    public interface IBridgeClientProvider
    {
        /// <summary>
        /// Gets the handle to the connected bridge or null if the connection is not present. This
        /// is for internal use only.
        /// </summary>
        SafeHandle? BridgeClient { get; }
    }
}
