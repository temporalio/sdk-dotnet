using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client.Interceptors;

namespace Temporalio.Client
{
    public partial class TemporalClient
    {
        /// <inheritdoc />
        public Task UpdateWorkerBuildIdCompatibilityAsync(
            string taskQueue,
            BuildIdOp buildIdOp,
            RpcOptions? rpcOptions) =>
            OutboundInterceptor.UpdateWorkerBuildIdCompatibilityAsync(new(
                TaskQueue: taskQueue,
                BuildIdOp: buildIdOp,
                RpcOptions: rpcOptions));

        /// <inheritdoc />
        public Task<WorkerBuildIdVersionSets?> GetWorkerBuildIdCompatibilityAsync(
            string taskQueue,
            int maxSets,
            RpcOptions? rpcOptions = null) =>
            OutboundInterceptor.GetWorkerBuildIdCompatibilityAsync(new(
                TaskQueue: taskQueue,
                MaxSets: maxSets,
                RpcOptions: rpcOptions));

        /// <inheritdoc />
        public Task<WorkerTaskReachability> GetWorkerTaskReachabilityAsync(
            IReadOnlyCollection<string> buildIds,
            IReadOnlyCollection<string> taskQueues,
            TaskReachabilityType? reachabilityType,
            RpcOptions? rpcOptions) =>
            OutboundInterceptor.GetWorkerTaskReachabilityAsync(new(
                BuildIds: buildIds,
                TaskQueues: taskQueues,
                ReachabilityType: reachabilityType,
                RpcOptions: rpcOptions));

        internal partial class Impl
        {
            /// <inheritdoc />
            public override async Task UpdateWorkerBuildIdCompatibilityAsync(
                UpdateWorkerBuildIdCompatibilityInput input)
            {
                var req = new UpdateWorkerBuildIdCompatibilityRequest
                {
                    Namespace = Client.Options.Namespace, TaskQueue = input.TaskQueue,
                };
                switch (input.BuildIdOp)
                {
                    case BuildIdOp.AddNewDefault op:
                        req.AddNewBuildIdInNewDefaultSet = op.BuildId;
                        break;
                    case BuildIdOp.AddNewCompatible op:
                        req.AddNewCompatibleBuildId =
                            new UpdateWorkerBuildIdCompatibilityRequest.Types.AddNewCompatibleVersion
                            {
                                NewBuildId = op.BuildId,
                                ExistingCompatibleBuildId = op.ExistingCompatibleBuildId,
                                MakeSetDefault = op.MakeSetDefault,
                            };
                        break;
                    case BuildIdOp.PromoteSetByBuildId op:
                        req.PromoteSetByBuildId = op.BuildId;
                        break;
                    case BuildIdOp.PromoteBuildIdWithinSet op:
                        req.PromoteBuildIdWithinSet = op.BuildId;
                        break;
                    case BuildIdOp.MergeSets op:
                        req.MergeSets = new UpdateWorkerBuildIdCompatibilityRequest.Types.MergeSets
                        {
                            PrimarySetBuildId = op.PrimaryBuildId, SecondarySetBuildId = op.SecondaryBuildId,
                        };
                        break;
                }

                await Client.Connection.WorkflowService
                    .UpdateWorkerBuildIdCompatibilityAsync(req, DefaultRetryOptions(input.RpcOptions))
                    .ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task<WorkerBuildIdVersionSets?> GetWorkerBuildIdCompatibilityAsync(
                GetWorkerBuildIdCompatibilityInput input)
            {
                var req = new GetWorkerBuildIdCompatibilityRequest
                {
                    Namespace = Client.Options.Namespace, TaskQueue = input.TaskQueue, MaxSets = input.MaxSets,
                };
                var resp = await Client.Connection.WorkflowService
                    .GetWorkerBuildIdCompatibilityAsync(req, DefaultRetryOptions(input.RpcOptions))
                    .ConfigureAwait(false);
                return WorkerBuildIdVersionSets.FromProto(resp);
            }

            /// <inheritdoc />
            public override async Task<WorkerTaskReachability> GetWorkerTaskReachabilityAsync(
                GetWorkerTaskReachabilityInput input)
            {
                var req = new GetWorkerTaskReachabilityRequest
                {
                    Namespace = Client.Options.Namespace,
                    Reachability = input.ReachabilityType?.ToProto() ?? TaskReachability.Unspecified,
                };
                req.BuildIds.AddRange(input.BuildIds);
                req.TaskQueues.AddRange(input.TaskQueues);
                var resp = await Client.Connection.WorkflowService
                    .GetWorkerTaskReachabilityAsync(req, DefaultRetryOptions(input.RpcOptions))
                    .ConfigureAwait(false);
                return WorkerTaskReachability.FromProto(resp);
            }
        }
    }
}