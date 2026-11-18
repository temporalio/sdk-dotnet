using System;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Bridge.Api.ActivityResult;
using Temporalio.Bridge.Api.ChildWorkflow;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCommands;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    /// <summary>
    /// Utilities to encode/decode workflow protobufs.
    /// </summary>
    internal static class WorkflowCodecHelper
    {
        /// <summary>
        /// Encode the completion.
        /// </summary>
        /// <param name="context">Codec context.</param>
        /// <param name="comp">Completion to encode.</param>
        /// <returns>Task for completion.</returns>
        internal static async Task EncodeAsync(
            WorkflowCodecContext context, WorkflowActivationCompletion comp)
        {
            switch (comp.StatusCase)
            {
                case WorkflowActivationCompletion.StatusOneofCase.Failed:
                    if (comp.Failed.Failure_ != null && context.CodecWorkflowContext != null)
                    {
                        await context.CodecWorkflowContext.EncodeFailureAsync(
                            comp.Failed.Failure_).ConfigureAwait(false);
                    }
                    break;
                case WorkflowActivationCompletion.StatusOneofCase.Successful:
                    foreach (var cmd in comp.Successful.Commands)
                    {
                        await EncodeAsync(context, cmd).ConfigureAwait(false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Decode the activation.
        /// </summary>
        /// <param name="context">Codec context.</param>
        /// <param name="act">Activation to decode.</param>
        /// <returns>Task for completion.</returns>
        internal static async Task DecodeAsync(WorkflowCodecContext context, WorkflowActivation act)
        {
            foreach (var job in act.Jobs)
            {
                var codec = JobCodec(context, job);
                if (codec == null)
                {
                    continue;
                }
                switch (job.VariantCase)
                {
                    case WorkflowActivationJob.VariantOneofCase.DoUpdate:
                        await DecodeAsync(codec, job.DoUpdate.Headers).ConfigureAwait(false);
                        await PayloadCodecHelper.DecodeAsync(codec, job.DoUpdate.Input).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.QueryWorkflow:
                        await PayloadCodecHelper.DecodeAsync(
                            codec, job.QueryWorkflow.Arguments).ConfigureAwait(false);
                        await DecodeAsync(codec, job.QueryWorkflow.Headers).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveActivity:
                        await DecodeAsync(codec, job.ResolveActivity.Result).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecution:
                        await DecodeAsync(
                            codec, job.ResolveChildWorkflowExecution.Result).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecutionStart:
                        if (job.ResolveChildWorkflowExecutionStart.Cancelled?.Failure is { } childStartFailure)
                        {
                            await codec.DecodeFailureAsync(childStartFailure).ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveNexusOperation:
                        if (job.ResolveNexusOperation.Result.Completed != null)
                        {
                            await PayloadCodecHelper.DecodeAsync(
                                codec, job.ResolveNexusOperation.Result.Completed).
                                ConfigureAwait(false);
                        }
                        else if (job.ResolveNexusOperation.Result.Failed != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveNexusOperation.Result.Failed).
                                ConfigureAwait(false);
                        }
                        else if (job.ResolveNexusOperation.Result.Cancelled != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveNexusOperation.Result.Cancelled).
                                ConfigureAwait(false);
                        }
                        else if (job.ResolveNexusOperation.Result.TimedOut != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveNexusOperation.Result.TimedOut).
                                ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveNexusOperationStart:
                        if (job.ResolveNexusOperationStart.Failed != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveNexusOperationStart.Failed).
                                ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveRequestCancelExternalWorkflow:
                        if (job.ResolveRequestCancelExternalWorkflow.Failure != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveRequestCancelExternalWorkflow.Failure).
                                ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveSignalExternalWorkflow:
                        if (job.ResolveSignalExternalWorkflow.Failure != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveSignalExternalWorkflow.Failure).
                                ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.SignalWorkflow:
                        await PayloadCodecHelper.DecodeAsync(
                            codec, job.SignalWorkflow.Input).ConfigureAwait(false);
                        await DecodeAsync(codec, job.SignalWorkflow.Headers).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.InitializeWorkflow:
                        await DecodeAsync(
                            codec,
                            PreviousRunCodec(context, job.InitializeWorkflow, codec),
                            job.InitializeWorkflow).ConfigureAwait(false);
                        break;
                }
            }
        }

        private static async Task EncodeAsync(WorkflowCodecContext context, WorkflowCommand cmd)
        {
            var codec = CommandCodec(context, cmd);
            if (codec == null)
            {
                return;
            }
            switch (cmd.VariantCase)
            {
                case WorkflowCommand.VariantOneofCase.CancelWorkflowExecution:
                    if (cmd.CancelWorkflowExecution.Details != null)
                    {
                        await PayloadCodecHelper.EncodeAsync(
                            codec,
                            cmd.CancelWorkflowExecution.Details.Payloads_).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.CompleteWorkflowExecution:
                    if (cmd.CompleteWorkflowExecution.Result != null)
                    {
                        await PayloadCodecHelper.EncodeAsync(
                            codec, cmd.CompleteWorkflowExecution.Result).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ContinueAsNewWorkflowExecution:
                    await PayloadCodecHelper.EncodeAsync(
                            codec, cmd.ContinueAsNewWorkflowExecution.Arguments).ConfigureAwait(false);
                    await EncodeAsync(
                        codec, cmd.ContinueAsNewWorkflowExecution.Memo).ConfigureAwait(false);
                    await EncodeAsync(
                        codec, cmd.ContinueAsNewWorkflowExecution.Headers).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.FailWorkflowExecution:
                    if (cmd.FailWorkflowExecution.Failure != null)
                    {
                        await codec.EncodeFailureAsync(
                            cmd.FailWorkflowExecution.Failure).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ModifyWorkflowProperties:
                    if (cmd.ModifyWorkflowProperties.UpsertedMemo != null)
                    {
                        await EncodeAsync(
                            codec, cmd.ModifyWorkflowProperties.UpsertedMemo.Fields).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.RespondToQuery:
                    if (cmd.RespondToQuery.Failed != null)
                    {
                        await codec.EncodeFailureAsync(
                            cmd.RespondToQuery.Failed).ConfigureAwait(false);
                    }
                    else if (cmd.RespondToQuery.Succeeded?.Response != null)
                    {
                        await PayloadCodecHelper.EncodeAsync(
                            codec, cmd.RespondToQuery.Succeeded.Response).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleActivity:
                    await PayloadCodecHelper.EncodeAsync(
                        codec, cmd.ScheduleActivity.Arguments).ConfigureAwait(false);
                    await EncodeAsync(codec, cmd.ScheduleActivity.Headers).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleLocalActivity:
                    await PayloadCodecHelper.EncodeAsync(
                        codec, cmd.ScheduleLocalActivity.Arguments).ConfigureAwait(false);
                    await EncodeAsync(
                        codec, cmd.ScheduleLocalActivity.Headers).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.SignalExternalWorkflowExecution:
                    await PayloadCodecHelper.EncodeAsync(
                        codec, cmd.SignalExternalWorkflowExecution.Args).ConfigureAwait(false);
                    await EncodeAsync(
                        codec, cmd.SignalExternalWorkflowExecution.Headers).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleNexusOperation:
                    if (cmd.ScheduleNexusOperation.Input != null)
                    {
                        await PayloadCodecHelper.EncodeAsync(
                            codec, cmd.ScheduleNexusOperation.Input).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.StartChildWorkflowExecution:
                    await PayloadCodecHelper.EncodeAsync(
                        codec, cmd.StartChildWorkflowExecution.Input).ConfigureAwait(false);
                    await EncodeAsync(
                        codec, cmd.StartChildWorkflowExecution.Memo).ConfigureAwait(false);
                    await EncodeAsync(
                        codec, cmd.StartChildWorkflowExecution.Headers).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.UpdateResponse:
                    if (cmd.UpdateResponse.Completed is { } updateCompleted)
                    {
                        await PayloadCodecHelper.EncodeAsync(codec, updateCompleted).ConfigureAwait(false);
                    }
                    else if (cmd.UpdateResponse.Rejected is { } updateRejected)
                    {
                        await codec.EncodeFailureAsync(updateRejected).ConfigureAwait(false);
                    }
                    break;
            }
            if (cmd.UserMetadata != null)
            {
                if (cmd.UserMetadata.Summary != null)
                {
                    await PayloadCodecHelper.EncodeAsync(
                        codec, cmd.UserMetadata.Summary).ConfigureAwait(false);
                }
                if (cmd.UserMetadata.Details != null)
                {
                    await PayloadCodecHelper.EncodeAsync(
                        codec, cmd.UserMetadata.Details).ConfigureAwait(false);
                }
            }
            foreach (var marker in cmd.EventGroupMarkers)
            {
                if (marker.Label?.Label_ is { } markerLabel)
                {
                    await PayloadCodecHelper.EncodeAsync(codec, markerLabel).ConfigureAwait(false);
                }
            }
        }

        private static async Task EncodeAsync(
            IPayloadCodec codec, MapField<string, Payload> payloads)
        {
            foreach (var val in payloads.Values)
            {
                if (val != null)
                {
                    await PayloadCodecHelper.EncodeAsync(codec, val).ConfigureAwait(false);
                }
            }
        }

        private static async Task DecodeAsync(IPayloadCodec codec, ActivityResolution res)
        {
            switch (res.StatusCase)
            {
                case ActivityResolution.StatusOneofCase.Cancelled:
                    if (res.Cancelled.Failure != null)
                    {
                        await codec.DecodeFailureAsync(res.Cancelled.Failure).ConfigureAwait(false);
                    }
                    break;
                case ActivityResolution.StatusOneofCase.Completed:
                    if (res.Completed.Result != null)
                    {
                        await PayloadCodecHelper.DecodeAsync(
                            codec, res.Completed.Result).ConfigureAwait(false);
                    }
                    break;
                case ActivityResolution.StatusOneofCase.Failed:
                    if (res.Failed.Failure_ != null)
                    {
                        await codec.DecodeFailureAsync(res.Failed.Failure_).ConfigureAwait(false);
                    }
                    break;
            }
        }

        private static async Task DecodeAsync(IPayloadCodec codec, ChildWorkflowResult res)
        {
            switch (res.StatusCase)
            {
                case ChildWorkflowResult.StatusOneofCase.Cancelled:
                    if (res.Cancelled.Failure != null)
                    {
                        await codec.DecodeFailureAsync(res.Cancelled.Failure).ConfigureAwait(false);
                    }
                    break;
                case ChildWorkflowResult.StatusOneofCase.Completed:
                    if (res.Completed.Result != null)
                    {
                        await PayloadCodecHelper.DecodeAsync(
                            codec, res.Completed.Result).ConfigureAwait(false);
                    }
                    break;
                case ChildWorkflowResult.StatusOneofCase.Failed:
                    if (res.Failed.Failure_ != null)
                    {
                        await codec.DecodeFailureAsync(res.Failed.Failure_).ConfigureAwait(false);
                    }
                    break;
            }
        }

        private static async Task DecodeAsync(
            IPayloadCodec codec, IPayloadCodec previousRunCodec, InitializeWorkflow init)
        {
            await PayloadCodecHelper.DecodeAsync(codec, init.Arguments).ConfigureAwait(false);
            if (init.ContinuedFailure != null)
            {
                await previousRunCodec.DecodeFailureAsync(init.ContinuedFailure).ConfigureAwait(false);
            }
            if (init.Memo != null)
            {
                await DecodeAsync(codec, init.Memo.Fields).ConfigureAwait(false);
            }
            await DecodeAsync(codec, init.Headers).ConfigureAwait(false);
            if (init.LastCompletionResult != null)
            {
                await PayloadCodecHelper.DecodeAsync(
                    previousRunCodec, init.LastCompletionResult.Payloads_).ConfigureAwait(false);
            }
        }

        private static async Task DecodeAsync(
            IPayloadCodec codec, MapField<string, Payload> payloads)
        {
            foreach (var val in payloads.Values)
            {
                if (val != null)
                {
                    await PayloadCodecHelper.DecodeAsync(codec, val).ConfigureAwait(false);
                }
            }
        }

        // Null when the job's payloads are not decoded.
        private static IPayloadCodec? JobCodec(WorkflowCodecContext context, WorkflowActivationJob job) =>
            job.VariantCase switch
            {
                WorkflowActivationJob.VariantOneofCase.ResolveActivity => WithContext(
                    context,
                    () => context.Instance?.GetPendingActivitySerializationContext(job.ResolveActivity.Seq)),
                WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecution => WithContext(
                    context,
                    () => context.Instance?.GetPendingChildSerializationContext(
                        job.ResolveChildWorkflowExecution.Seq)),
                WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecutionStart => WithContext(
                    context,
                    () => context.Instance?.GetPendingChildSerializationContext(
                        job.ResolveChildWorkflowExecutionStart.Seq)),
                WorkflowActivationJob.VariantOneofCase.ResolveNexusOperation => WithContext(
                    context,
                    () => context.Instance?.GetPendingNexusOperationSerializationContext(
                        job.ResolveNexusOperation.Seq)),
                WorkflowActivationJob.VariantOneofCase.ResolveNexusOperationStart => WithContext(
                    context,
                    () => context.Instance?.GetPendingNexusOperationSerializationContext(
                        job.ResolveNexusOperationStart.Seq)),
                WorkflowActivationJob.VariantOneofCase.ResolveRequestCancelExternalWorkflow => WithContext(
                    context,
                    () => context.Instance?.GetPendingExternalCancelSerializationContext(
                        job.ResolveRequestCancelExternalWorkflow.Seq)),
                WorkflowActivationJob.VariantOneofCase.ResolveSignalExternalWorkflow => WithContext(
                    context,
                    () => context.Instance?.GetPendingExternalSignalSerializationContext(
                        job.ResolveSignalExternalWorkflow.Seq)),
                _ => context.CodecWorkflowContext,
            };

        // Covers the command's user metadata and event group markers too, which sit on the command
        // rather than its variant. Null when the command's payloads are not encoded.
        private static IPayloadCodec? CommandCodec(WorkflowCodecContext context, WorkflowCommand cmd)
        {
            switch (cmd.VariantCase)
            {
                case WorkflowCommand.VariantOneofCase.CompleteWorkflowExecution:
                case WorkflowCommand.VariantOneofCase.FailWorkflowExecution:
                    return ResultCodec(context);
                case WorkflowCommand.VariantOneofCase.ScheduleActivity:
                    return WithContext(
                        context,
                        () => new ISerializationContext.Activity(
                            Namespace: context.Namespace,
                            ActivityId: cmd.ScheduleActivity.ActivityId,
                            WorkflowId: context.WorkflowId,
                            WorkflowType: context.WorkflowType,
                            ActivityType: cmd.ScheduleActivity.ActivityType,
                            ActivityTaskQueue: cmd.ScheduleActivity.TaskQueue ?? context.TaskQueue,
                            IsLocal: false));
                case WorkflowCommand.VariantOneofCase.ScheduleLocalActivity:
                    return WithContext(
                        context,
                        () => new ISerializationContext.Activity(
                            Namespace: context.Namespace,
                            ActivityId: cmd.ScheduleLocalActivity.ActivityId,
                            WorkflowId: context.WorkflowId,
                            WorkflowType: context.WorkflowType,
                            ActivityType: cmd.ScheduleLocalActivity.ActivityType,
                            ActivityTaskQueue: context.TaskQueue,
                            IsLocal: true));
                case WorkflowCommand.VariantOneofCase.SignalExternalWorkflowExecution:
                    return WithContext(
                        context,
                        () =>
                        {
                            var signal = cmd.SignalExternalWorkflowExecution;
                            var workflowId = signal.HasChildWorkflowId ?
                                signal.ChildWorkflowId : signal.WorkflowExecution.WorkflowId;
                            return new ISerializationContext.Workflow(
                                Namespace: context.Namespace, WorkflowId: workflowId);
                        });
                case WorkflowCommand.VariantOneofCase.ScheduleNexusOperation:
                    return WithContext(
                        context,
                        () => context.Instance?.GetPendingNexusOperationSerializationContext(
                            cmd.ScheduleNexusOperation.Seq));
                case WorkflowCommand.VariantOneofCase.StartChildWorkflowExecution:
                    return WithContext(
                        context,
                        () => new ISerializationContext.Workflow(
                            Namespace: context.Namespace,
                            WorkflowId: cmd.StartChildWorkflowExecution.WorkflowId));
                case WorkflowCommand.VariantOneofCase.UpdateResponse:
                    // An update sent by a Nexus operation handler returns its result and failure
                    // to the Nexus caller, which decodes them with the handler's Nexus context
                    return context.CodecNoContext is IWithSerializationContext<IPayloadCodec> withUpdate &&
                        context.Instance?.GetUpdateNexusSerializationContext(
                            cmd.UpdateResponse.ProtocolInstanceId) is { } updateContext ?
                        withUpdate.WithSerializationContext(updateContext) : context.CodecWorkflowContext;
                default:
                    return context.CodecWorkflowContext;
            }
        }

        // A retry or cron run receives the previous run's closing failure or result, which that run
        // encoded with the Nexus context of the handler that started it. The server carries the
        // context over to the new run. The instance does not exist yet on this activation, so the
        // context comes from the job.
        private static IPayloadCodec PreviousRunCodec(
            WorkflowCodecContext context, InitializeWorkflow init, IPayloadCodec workflowCodec) =>
            context.CodecNoContext is IWithSerializationContext<IPayloadCodec> withContext &&
                ISerializationContext.Nexus.FromProto(init.PropagatedNexusSerializationContext) is { } nexusContext ?
                withContext.WithSerializationContext(nexusContext) : workflowCodec;

        // Without a codec that accepts a serialization context, or a context to give it, the codec
        // is used as it is. The context is only built for a codec that accepts one.
        private static IPayloadCodec? WithContext(
            WorkflowCodecContext context, Func<ISerializationContext?> serializationContext) =>
            context.CodecNoContext is IWithSerializationContext<IPayloadCodec> withContext &&
                serializationContext() is { } built ?
                withContext.WithSerializationContext(built) : context.CodecNoContext;

        // A workflow started by a Nexus operation handler returns its result and closing failure
        // to the Nexus caller, which decodes them with the handler's Nexus context
        private static IPayloadCodec? ResultCodec(WorkflowCodecContext context) =>
            context.CodecNoContext is IWithSerializationContext<IPayloadCodec> withContext &&
                context.Instance?.NexusSerializationContext is { } nexusContext ?
                withContext.WithSerializationContext(nexusContext) : context.CodecWorkflowContext;

        internal record WorkflowCodecContext(
            IPayloadCodec? CodecNoContext,
            IPayloadCodec? CodecWorkflowContext,
            string Namespace,
            string WorkflowId,
            string WorkflowType,
            string TaskQueue,
            IWorkflowCodecHelperInstance? Instance);
    }
}
