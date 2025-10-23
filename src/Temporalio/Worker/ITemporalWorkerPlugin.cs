using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Worker
{
    public interface ITemporalWorkerPlugin
    {
        public string Name { get; }

        public void ConfigureWorker(TemporalWorkerOptions options);

        public Task RunWorker(TemporalWorker worker, Func<TemporalWorker, Task> continuation);

        public void ConfigureReplayer(WorkflowReplayerOptions options);

        public Task<IEnumerable<WorkflowReplayResult>> RunReplayer(WorkflowReplayer replayer, Func<WorkflowReplayer, Task<IEnumerable<WorkflowReplayResult>>> continuation);
    }
}