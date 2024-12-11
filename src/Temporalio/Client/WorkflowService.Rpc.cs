// <auto-generated>
//     Generated. DO NOT EDIT!
// </auto-generated>
#pragma warning disable 8669
using System.Threading.Tasks;
using Temporalio.Api.WorkflowService.V1;

namespace Temporalio.Client
{
    public abstract partial class WorkflowService
    {
        /// <summary>
        /// Invoke CountWorkflowExecutions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CountWorkflowExecutionsResponse> CountWorkflowExecutionsAsync(CountWorkflowExecutionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CountWorkflowExecutions", req, CountWorkflowExecutionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateSchedule.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateScheduleResponse> CreateScheduleAsync(CreateScheduleRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateSchedule", req, CreateScheduleResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteSchedule.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteScheduleResponse> DeleteScheduleAsync(DeleteScheduleRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteSchedule", req, DeleteScheduleResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteWorkflowExecutionResponse> DeleteWorkflowExecutionAsync(DeleteWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteWorkflowExecution", req, DeleteWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeprecateNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeprecateNamespaceResponse> DeprecateNamespaceAsync(DeprecateNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeprecateNamespace", req, DeprecateNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DescribeBatchOperation.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DescribeBatchOperationResponse> DescribeBatchOperationAsync(DescribeBatchOperationRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DescribeBatchOperation", req, DescribeBatchOperationResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DescribeNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DescribeNamespaceResponse> DescribeNamespaceAsync(DescribeNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DescribeNamespace", req, DescribeNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DescribeSchedule.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DescribeScheduleResponse> DescribeScheduleAsync(DescribeScheduleRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DescribeSchedule", req, DescribeScheduleResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DescribeTaskQueue.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DescribeTaskQueueResponse> DescribeTaskQueueAsync(DescribeTaskQueueRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DescribeTaskQueue", req, DescribeTaskQueueResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DescribeWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DescribeWorkflowExecutionResponse> DescribeWorkflowExecutionAsync(DescribeWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DescribeWorkflowExecution", req, DescribeWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ExecuteMultiOperation.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ExecuteMultiOperationResponse> ExecuteMultiOperationAsync(ExecuteMultiOperationRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ExecuteMultiOperation", req, ExecuteMultiOperationResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetClusterInfo.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetClusterInfoResponse> GetClusterInfoAsync(GetClusterInfoRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetClusterInfo", req, GetClusterInfoResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetSearchAttributes.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetSearchAttributesResponse> GetSearchAttributesAsync(GetSearchAttributesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetSearchAttributes", req, GetSearchAttributesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetSystemInfo.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetSystemInfoResponse> GetSystemInfoAsync(GetSystemInfoRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetSystemInfo", req, GetSystemInfoResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetWorkerBuildIdCompatibility.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetWorkerBuildIdCompatibilityResponse> GetWorkerBuildIdCompatibilityAsync(GetWorkerBuildIdCompatibilityRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetWorkerBuildIdCompatibility", req, GetWorkerBuildIdCompatibilityResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetWorkerTaskReachability.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetWorkerTaskReachabilityResponse> GetWorkerTaskReachabilityAsync(GetWorkerTaskReachabilityRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetWorkerTaskReachability", req, GetWorkerTaskReachabilityResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetWorkerVersioningRules.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetWorkerVersioningRulesResponse> GetWorkerVersioningRulesAsync(GetWorkerVersioningRulesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetWorkerVersioningRules", req, GetWorkerVersioningRulesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetWorkflowExecutionHistory.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetWorkflowExecutionHistoryResponse> GetWorkflowExecutionHistoryAsync(GetWorkflowExecutionHistoryRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetWorkflowExecutionHistory", req, GetWorkflowExecutionHistoryResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetWorkflowExecutionHistoryReverse.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetWorkflowExecutionHistoryReverseResponse> GetWorkflowExecutionHistoryReverseAsync(GetWorkflowExecutionHistoryReverseRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetWorkflowExecutionHistoryReverse", req, GetWorkflowExecutionHistoryReverseResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListArchivedWorkflowExecutions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListArchivedWorkflowExecutionsResponse> ListArchivedWorkflowExecutionsAsync(ListArchivedWorkflowExecutionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListArchivedWorkflowExecutions", req, ListArchivedWorkflowExecutionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListBatchOperations.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListBatchOperationsResponse> ListBatchOperationsAsync(ListBatchOperationsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListBatchOperations", req, ListBatchOperationsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListClosedWorkflowExecutions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListClosedWorkflowExecutionsResponse> ListClosedWorkflowExecutionsAsync(ListClosedWorkflowExecutionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListClosedWorkflowExecutions", req, ListClosedWorkflowExecutionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListNamespaces.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListNamespacesResponse> ListNamespacesAsync(ListNamespacesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListNamespaces", req, ListNamespacesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListOpenWorkflowExecutions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListOpenWorkflowExecutionsResponse> ListOpenWorkflowExecutionsAsync(ListOpenWorkflowExecutionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListOpenWorkflowExecutions", req, ListOpenWorkflowExecutionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListScheduleMatchingTimes.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListScheduleMatchingTimesResponse> ListScheduleMatchingTimesAsync(ListScheduleMatchingTimesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListScheduleMatchingTimes", req, ListScheduleMatchingTimesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListSchedules.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListSchedulesResponse> ListSchedulesAsync(ListSchedulesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListSchedules", req, ListSchedulesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListTaskQueuePartitions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListTaskQueuePartitionsResponse> ListTaskQueuePartitionsAsync(ListTaskQueuePartitionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListTaskQueuePartitions", req, ListTaskQueuePartitionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ListWorkflowExecutions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ListWorkflowExecutionsResponse> ListWorkflowExecutionsAsync(ListWorkflowExecutionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ListWorkflowExecutions", req, ListWorkflowExecutionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke PatchSchedule.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<PatchScheduleResponse> PatchScheduleAsync(PatchScheduleRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("PatchSchedule", req, PatchScheduleResponse.Parser, options);
        }

        /// <summary>
        /// Invoke PauseActivityById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<PauseActivityByIdResponse> PauseActivityByIdAsync(PauseActivityByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("PauseActivityById", req, PauseActivityByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke PollActivityTaskQueue.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<PollActivityTaskQueueResponse> PollActivityTaskQueueAsync(PollActivityTaskQueueRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("PollActivityTaskQueue", req, PollActivityTaskQueueResponse.Parser, options);
        }

        /// <summary>
        /// Invoke PollNexusTaskQueue.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<PollNexusTaskQueueResponse> PollNexusTaskQueueAsync(PollNexusTaskQueueRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("PollNexusTaskQueue", req, PollNexusTaskQueueResponse.Parser, options);
        }

        /// <summary>
        /// Invoke PollWorkflowExecutionUpdate.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<PollWorkflowExecutionUpdateResponse> PollWorkflowExecutionUpdateAsync(PollWorkflowExecutionUpdateRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("PollWorkflowExecutionUpdate", req, PollWorkflowExecutionUpdateResponse.Parser, options);
        }

        /// <summary>
        /// Invoke PollWorkflowTaskQueue.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<PollWorkflowTaskQueueResponse> PollWorkflowTaskQueueAsync(PollWorkflowTaskQueueRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("PollWorkflowTaskQueue", req, PollWorkflowTaskQueueResponse.Parser, options);
        }

        /// <summary>
        /// Invoke QueryWorkflow.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<QueryWorkflowResponse> QueryWorkflowAsync(QueryWorkflowRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("QueryWorkflow", req, QueryWorkflowResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RecordActivityTaskHeartbeat.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RecordActivityTaskHeartbeatResponse> RecordActivityTaskHeartbeatAsync(RecordActivityTaskHeartbeatRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RecordActivityTaskHeartbeat", req, RecordActivityTaskHeartbeatResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RecordActivityTaskHeartbeatById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RecordActivityTaskHeartbeatByIdResponse> RecordActivityTaskHeartbeatByIdAsync(RecordActivityTaskHeartbeatByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RecordActivityTaskHeartbeatById", req, RecordActivityTaskHeartbeatByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RegisterNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RegisterNamespaceResponse> RegisterNamespaceAsync(RegisterNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RegisterNamespace", req, RegisterNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RequestCancelWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RequestCancelWorkflowExecutionResponse> RequestCancelWorkflowExecutionAsync(RequestCancelWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RequestCancelWorkflowExecution", req, RequestCancelWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ResetActivityById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ResetActivityByIdResponse> ResetActivityByIdAsync(ResetActivityByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ResetActivityById", req, ResetActivityByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ResetStickyTaskQueue.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ResetStickyTaskQueueResponse> ResetStickyTaskQueueAsync(ResetStickyTaskQueueRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ResetStickyTaskQueue", req, ResetStickyTaskQueueResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ResetWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ResetWorkflowExecutionResponse> ResetWorkflowExecutionAsync(ResetWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ResetWorkflowExecution", req, ResetWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondActivityTaskCanceled.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondActivityTaskCanceledResponse> RespondActivityTaskCanceledAsync(RespondActivityTaskCanceledRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondActivityTaskCanceled", req, RespondActivityTaskCanceledResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondActivityTaskCanceledById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondActivityTaskCanceledByIdResponse> RespondActivityTaskCanceledByIdAsync(RespondActivityTaskCanceledByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondActivityTaskCanceledById", req, RespondActivityTaskCanceledByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondActivityTaskCompleted.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondActivityTaskCompletedResponse> RespondActivityTaskCompletedAsync(RespondActivityTaskCompletedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondActivityTaskCompleted", req, RespondActivityTaskCompletedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondActivityTaskCompletedById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondActivityTaskCompletedByIdResponse> RespondActivityTaskCompletedByIdAsync(RespondActivityTaskCompletedByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondActivityTaskCompletedById", req, RespondActivityTaskCompletedByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondActivityTaskFailed.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondActivityTaskFailedResponse> RespondActivityTaskFailedAsync(RespondActivityTaskFailedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondActivityTaskFailed", req, RespondActivityTaskFailedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondActivityTaskFailedById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondActivityTaskFailedByIdResponse> RespondActivityTaskFailedByIdAsync(RespondActivityTaskFailedByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondActivityTaskFailedById", req, RespondActivityTaskFailedByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondNexusTaskCompleted.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondNexusTaskCompletedResponse> RespondNexusTaskCompletedAsync(RespondNexusTaskCompletedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondNexusTaskCompleted", req, RespondNexusTaskCompletedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondNexusTaskFailed.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondNexusTaskFailedResponse> RespondNexusTaskFailedAsync(RespondNexusTaskFailedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondNexusTaskFailed", req, RespondNexusTaskFailedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondQueryTaskCompleted.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondQueryTaskCompletedResponse> RespondQueryTaskCompletedAsync(RespondQueryTaskCompletedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondQueryTaskCompleted", req, RespondQueryTaskCompletedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondWorkflowTaskCompleted.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondWorkflowTaskCompletedResponse> RespondWorkflowTaskCompletedAsync(RespondWorkflowTaskCompletedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondWorkflowTaskCompleted", req, RespondWorkflowTaskCompletedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RespondWorkflowTaskFailed.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RespondWorkflowTaskFailedResponse> RespondWorkflowTaskFailedAsync(RespondWorkflowTaskFailedRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RespondWorkflowTaskFailed", req, RespondWorkflowTaskFailedResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ScanWorkflowExecutions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ScanWorkflowExecutionsResponse> ScanWorkflowExecutionsAsync(ScanWorkflowExecutionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ScanWorkflowExecutions", req, ScanWorkflowExecutionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ShutdownWorker.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ShutdownWorkerResponse> ShutdownWorkerAsync(ShutdownWorkerRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ShutdownWorker", req, ShutdownWorkerResponse.Parser, options);
        }

        /// <summary>
        /// Invoke SignalWithStartWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<SignalWithStartWorkflowExecutionResponse> SignalWithStartWorkflowExecutionAsync(SignalWithStartWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("SignalWithStartWorkflowExecution", req, SignalWithStartWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke SignalWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<SignalWorkflowExecutionResponse> SignalWorkflowExecutionAsync(SignalWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("SignalWorkflowExecution", req, SignalWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke StartBatchOperation.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<StartBatchOperationResponse> StartBatchOperationAsync(StartBatchOperationRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("StartBatchOperation", req, StartBatchOperationResponse.Parser, options);
        }

        /// <summary>
        /// Invoke StartWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<StartWorkflowExecutionResponse> StartWorkflowExecutionAsync(StartWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("StartWorkflowExecution", req, StartWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke StopBatchOperation.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<StopBatchOperationResponse> StopBatchOperationAsync(StopBatchOperationRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("StopBatchOperation", req, StopBatchOperationResponse.Parser, options);
        }

        /// <summary>
        /// Invoke TerminateWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<TerminateWorkflowExecutionResponse> TerminateWorkflowExecutionAsync(TerminateWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("TerminateWorkflowExecution", req, TerminateWorkflowExecutionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UnpauseActivityById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UnpauseActivityByIdResponse> UnpauseActivityByIdAsync(UnpauseActivityByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UnpauseActivityById", req, UnpauseActivityByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateActivityOptionsById.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateActivityOptionsByIdResponse> UpdateActivityOptionsByIdAsync(UpdateActivityOptionsByIdRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateActivityOptionsById", req, UpdateActivityOptionsByIdResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateNamespaceResponse> UpdateNamespaceAsync(UpdateNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateNamespace", req, UpdateNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateSchedule.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateScheduleResponse> UpdateScheduleAsync(UpdateScheduleRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateSchedule", req, UpdateScheduleResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateWorkerBuildIdCompatibility.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateWorkerBuildIdCompatibilityResponse> UpdateWorkerBuildIdCompatibilityAsync(UpdateWorkerBuildIdCompatibilityRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateWorkerBuildIdCompatibility", req, UpdateWorkerBuildIdCompatibilityResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateWorkerVersioningRules.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateWorkerVersioningRulesResponse> UpdateWorkerVersioningRulesAsync(UpdateWorkerVersioningRulesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateWorkerVersioningRules", req, UpdateWorkerVersioningRulesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateWorkflowExecution.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateWorkflowExecutionResponse> UpdateWorkflowExecutionAsync(UpdateWorkflowExecutionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateWorkflowExecution", req, UpdateWorkflowExecutionResponse.Parser, options);
        }
    }
}