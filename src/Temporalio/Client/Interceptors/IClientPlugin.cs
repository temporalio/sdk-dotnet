using System.Threading.Tasks;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Plugin for client calls.
    /// </summary>
    public interface IClientPlugin
    {
        public void InitClientPlugin(IClientPlugin nextPlugin);

        public TemporalClientOptions OnCreateClient(TemporalClientOptions options);

        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options);

        public TemporalConnection TemporalConnect(TemporalClientConnectOptions options);
    }
}