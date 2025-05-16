using System;

namespace Temporalio.Workflows
{
    public class NexusClientOptions : ICloneable
    {
        public NexusClientOptions()
        {
        }

        public NexusClientOptions(string endpoint) => Endpoint = endpoint;

        public string? Endpoint { get; set; }

        public virtual object Clone() => MemberwiseClone();
    }
}