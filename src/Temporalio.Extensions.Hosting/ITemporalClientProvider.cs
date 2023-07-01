using System.Threading.Tasks;
using Temporalio.Client;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Interface for providing a lazily instantiated <see cref="ITemporalClient" />.
    /// </summary>
    public interface ITemporalClientProvider
    {
        /// <summary>
        /// Get the client.
        /// </summary>
        Task<ITemporalClient> GetClient();
    }
}