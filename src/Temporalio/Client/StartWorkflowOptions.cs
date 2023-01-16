using System;
using Temporalio.Api.Enums.V1;
using System.Collections.Generic;

namespace Temporalio.Client {
    public class StartWorkflowOptions : ICloneable {
        public string? ID { get; set; }

        public string? TaskQueue { get; set; }

        public TimeSpan? ExecutionTimeout { get; set; }

        public TimeSpan? RunTimeout { get; set; }

        public TimeSpan? TaskTimeout { get; set; }

        public WorkflowIdReusePolicy IDReusePolicy { get; set; } = WorkflowIdReusePolicy.AllowDuplicate;

        public string? CronSchedule { get; set; }

        public IEnumerable<KeyValuePair<string, object>>? Memo { get; set; }

        public SearchAttributeDictionary? SearchAttributes { get; set; }

        public string? StartSignal { get; set; }

        public object[]? StartSignalArgs { get; set; }

        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}