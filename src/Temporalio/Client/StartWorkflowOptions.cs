using System;
using Temporalio.Api.Enums.V1;
using System.Collections.Generic;

namespace Temporalio.Client {
    public class StartWorkflowOptions : ICloneable {

        public StartWorkflowOptions() { }

        public StartWorkflowOptions(string id, string taskQueue) {
            ID = id;
            TaskQueue = taskQueue;
        }

        public string? ID { get; set; }

        public string? TaskQueue { get; set; }

        public TimeSpan? ExecutionTimeout { get; set; }

        public TimeSpan? RunTimeout { get; set; }

        public TimeSpan? TaskTimeout { get; set; }

        public WorkflowIdReusePolicy IDReusePolicy { get; set; } = WorkflowIdReusePolicy.AllowDuplicate;

        public RetryPolicy? RetryPolicy { get; set; }

        public string? CronSchedule { get; set; }

        public IReadOnlyCollection<KeyValuePair<string, object>>? Memo { get; set; }

        public SearchAttributeDictionary? SearchAttributes { get; set; }

        public string? StartSignal { get; set; }

        public IReadOnlyCollection<object?>? StartSignalArgs { get; set; }

        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (StartWorkflowOptions)MemberwiseClone();
            if (Rpc != null) {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}