using System;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public interface ITemporalClientPlugin
    {
        public string Name { get; }

        public void ConfigureClient(TemporalClientOptions options);

        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options, Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation);
    }
}