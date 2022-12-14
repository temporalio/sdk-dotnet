using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public interface IBridgeClientProvider
    {
        public Task<SafeHandle> ConnectedBridgeClientAsync();
    }
}
