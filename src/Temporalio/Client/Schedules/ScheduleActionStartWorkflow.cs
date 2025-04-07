using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Schedule action to start a workflow. Instead of the constructor, most users should use the
    /// <c>Create</c> static method to create a typed workflow invocation.
    /// </summary>
    /// <param name="Workflow">Workflow type name.</param>
    /// <param name="Args">Arguments for the workflow. Note, when fetching this from the server,
    /// the every value here is an instance of <see cref="IEncodedRawValue" />.</param>
    /// <param name="Options">Start workflow options. ID and TaskQueue are required. Some options
    /// like ID reuse policy, cron schedule, and start signal cannot be set or an error will occur.
    /// </param>
    /// <param name="Headers">Headers sent with each workflow scheduled.</param>
    public record ScheduleActionStartWorkflow(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        WorkflowOptions Options,
        IReadOnlyDictionary<string, IEncodedRawValue>? Headers = null) : ScheduleAction
    {
        /// <summary>
        /// Create a scheduled action that starts a workflow via lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Result type of the workflow.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required. Some
        /// options like ID reuse policy, cron schedule, and start signal cannot be set or an error
        /// will occur.</param>
        /// <returns>Start workflow action.</returns>
        public static ScheduleActionStartWorkflow Create<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = ExpressionUtil.ExtractCall(workflowRunCall);
            return Create(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Create a scheduled action that starts a workflow via lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method without a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required. Some
        /// options like ID reuse policy, cron schedule, and start signal cannot be set or an error
        /// will occur.</param>
        /// <returns>Start workflow action.</returns>
        public static ScheduleActionStartWorkflow Create<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = ExpressionUtil.ExtractCall(workflowRunCall);
            return Create(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Create a scheduled action that starts a workflow.
        /// </summary>
        /// <param name="workflow">Workflow run method.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required. Some
        /// options like ID reuse policy, cron schedule, and start signal cannot be set or an error
        /// will occur.</param>
        /// <returns>Start workflow action.</returns>
        public static ScheduleActionStartWorkflow Create(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options) =>
            new(Workflow: workflow, Args: args, Options: options);

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <returns>Converted value.</returns>
        internal static async Task<ScheduleActionStartWorkflow> FromProtoAsync(
            Api.Workflow.V1.NewWorkflowExecutionInfo proto, string clientNamespace, DataConverter dataConverter)
        {
            // Workflow-specific data converter
            dataConverter = dataConverter.WithSerializationContext(
                new ISerializationContext.Workflow(
                    Namespace: clientNamespace,
                    WorkflowId: proto.WorkflowId));

            IReadOnlyCollection<object?> args = proto.Input == null ?
                Array.Empty<object?>() :
                proto.Input.Payloads_.Select(p => new EncodedRawValue(dataConverter, p)).ToList();
            var headers = proto.Header?.Fields?.ToDictionary(
                kvp => kvp.Key,
                kvp => (IEncodedRawValue)new EncodedRawValue(dataConverter, kvp.Value));
            var (staticSummary, staticDetails) =
                await dataConverter.FromUserMetadataAsync(proto.UserMetadata).ConfigureAwait(false);
            return new(
                Workflow: proto.WorkflowType.Name,
                Args: args,
                Options: new(id: proto.WorkflowId, taskQueue: proto.TaskQueue.Name)
                {
                    ExecutionTimeout = proto.WorkflowExecutionTimeout?.ToTimeSpan(),
                    RunTimeout = proto.WorkflowRunTimeout?.ToTimeSpan(),
                    TaskTimeout = proto.WorkflowTaskTimeout?.ToTimeSpan(),
                    RetryPolicy = proto.RetryPolicy == null ? null : Common.RetryPolicy.FromProto(proto.RetryPolicy),
                    Memo = proto.Memo == null ? new Dictionary<string, object>(0) :
                        proto.Memo.Fields.ToDictionary(
                            kvp => kvp.Key,
                            kvp => (object)new EncodedRawValue(dataConverter, kvp.Value)),
                    TypedSearchAttributes = proto.SearchAttributes == null ?
                        SearchAttributeCollection.Empty :
                        SearchAttributeCollection.FromProto(proto.SearchAttributes),
                    StaticSummary = staticSummary,
                    StaticDetails = staticDetails,
                },
                Headers: headers);
        }

        /// <inheritdoc />
        internal override async Task<Api.Schedule.V1.ScheduleAction> ToProtoAsync(
            string clientNamespace, DataConverter dataConverter)
        {
            // Disallow some options
            if (Options.IdReusePolicy != Api.Enums.V1.WorkflowIdReusePolicy.AllowDuplicate)
            {
                throw new ArgumentException("ID reuse policy cannot change from default for scheduled workflow");
            }
            if (Options.IdConflictPolicy != Api.Enums.V1.WorkflowIdConflictPolicy.Unspecified)
            {
                throw new ArgumentException("ID conflict policy cannot change from default for scheduled workflow");
            }
            if (Options.CronSchedule != null)
            {
                throw new ArgumentException("Cron schedule cannot be set on scheduled workflow");
            }
            if (Options.StartSignal != null || Options.StartSignalArgs != null)
            {
                throw new ArgumentException("Start signal and/or start signal args cannot be set on scheduled workflow");
            }
            if (Options.Rpc != null)
            {
                throw new ArgumentException("RPC options cannot be set on scheduled workflow");
            }
            // Workflow-specific data converter
            dataConverter = dataConverter.WithSerializationContext(
                new ISerializationContext.Workflow(
                    Namespace: clientNamespace,
                    WorkflowId: Options.Id ?? throw new ArgumentException("ID required on workflow action")));

            // Build input. We have to go one payload at a time here because half could be encoded
            // and half not (e.g. they just changed the second parameter).
            var input = await Task.WhenAll(Args.Select(arg =>
            {
                if (arg is IEncodedRawValue raw)
                {
                    return Task.FromResult(raw.Payload);
                }
                return dataConverter.ToPayloadAsync(arg);
            }).ToList()).ConfigureAwait(false);

            var workflow = new Api.Workflow.V1.NewWorkflowExecutionInfo()
            {
                WorkflowId = Options.Id,
                WorkflowType = new() { Name = Workflow },
                TaskQueue = new()
                {
                    Name = Options.TaskQueue ??
                        throw new ArgumentException("Task queue required on workflow action"),
                },
                Input = Args.Count == 0 ? null : new() { Payloads_ = { input } },
                WorkflowExecutionTimeout = Options.ExecutionTimeout is TimeSpan execTimeout ?
                    Duration.FromTimeSpan(execTimeout) : null,
                WorkflowRunTimeout = Options.RunTimeout is TimeSpan runTimeout ?
                    Duration.FromTimeSpan(runTimeout) : null,
                WorkflowTaskTimeout = Options.TaskTimeout is TimeSpan taskTimeout ?
                    Duration.FromTimeSpan(taskTimeout) : null,
                RetryPolicy = Options.RetryPolicy?.ToProto(),
                UserMetadata = await dataConverter.ToUserMetadataAsync(
                    Options.StaticSummary, Options.StaticDetails).ConfigureAwait(false),
            };
            if (Options.Memo != null && Options.Memo.Count > 0)
            {
                workflow.Memo = new();
                foreach (var field in Options.Memo)
                {
                    if (field.Value == null)
                    {
                        throw new ArgumentException($"Memo value for {field.Key} is null");
                    }
                    var value = field.Value is IEncodedRawValue raw ? raw.Payload :
                        await dataConverter.ToPayloadAsync(field.Value).ConfigureAwait(false);
                    workflow.Memo.Fields.Add(field.Key, value);
                }
            }
            if (Options.TypedSearchAttributes != null && Options.TypedSearchAttributes.Count > 0)
            {
                workflow.SearchAttributes = Options.TypedSearchAttributes.ToProto();
            }
            if (Headers != null)
            {
                workflow.Header = new();
                foreach (var pair in Headers)
                {
                    workflow.Header.Fields.Add(pair.Key, pair.Value.Payload);
                }
            }

            return new() { StartWorkflow = workflow };
        }
    }
}