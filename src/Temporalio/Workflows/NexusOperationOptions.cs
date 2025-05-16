using System;
using System.Threading;

namespace Temporalio.Workflows
{
    public class NexusOperationOptions : ICloneable
    {
        public NexusOperationOptions()
        {
        }

        public string? OperationName { get; set; }

        public TimeSpan? ScheduleToCloseTimeout { get; set; }

        public string? Summary { get; set; }

        public CancellationToken? CancellationToken { get; set; }

        // TODO(cretz): Cancellation type

        public virtual object Clone() => MemberwiseClone();
    }
}