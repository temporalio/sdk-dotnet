using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
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
                switch (job.VariantCase)
                {
                    case WorkflowActivationJob.VariantOneofCase.DoUpdate:
                        if (context.CodecWorkflowContext != null)
                        {
                            await DecodeAsync(context.CodecWorkflowContext, job.DoUpdate.Headers).ConfigureAwait(false);
                            await DecodeAsync(context.CodecWorkflowContext, job.DoUpdate.Input).ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.QueryWorkflow:
                        if (context.CodecWorkflowContext != null)
                        {
                            await DecodeAsync(context.CodecWorkflowContext, job.QueryWorkflow.Arguments).ConfigureAwait(false);
                            await DecodeAsync(context.CodecWorkflowContext, job.QueryWorkflow.Headers).ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveActivity:
                        // Apply activity context
                        var actCodec = context.CodecNoContext;
                        if (actCodec is IWithSerializationContext<IPayloadCodec> withAct &&
                            context.Instance?.GetPendingActivitySerializationContext(job.ResolveActivity.Seq) is { } actContext)
                        {
                            actCodec = withAct.WithSerializationContext(actContext);
                        }
                        if (actCodec != null)
                        {
                            await DecodeAsync(actCodec, job.ResolveActivity.Result).ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecution:
                        // Apply child workflow context
                        var childCodec = context.CodecNoContext;
                        if (childCodec is IWithSerializationContext<IPayloadCodec> withChild &&
                            context.Instance?.GetPendingChildSerializationContext(job.ResolveChildWorkflowExecution.Seq) is { } childContext)
                        {
                            childCodec = withChild.WithSerializationContext(childContext);
                        }
                        if (childCodec != null)
                        {
                            await DecodeAsync(
                                childCodec, job.ResolveChildWorkflowExecution.Result).ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecutionStart:
                        if (job.ResolveChildWorkflowExecutionStart.Cancelled != null
                            && job.ResolveChildWorkflowExecutionStart.Cancelled.Failure != null)
                        {
                            // Apply child workflow context
                            var childCodec2 = context.CodecNoContext;
                            if (childCodec2 is IWithSerializationContext<IPayloadCodec> withChild2 &&
                                context.Instance?.GetPendingChildSerializationContext(job.ResolveChildWorkflowExecution.Seq) is { } childContext2)
                            {
                                childCodec2 = withChild2.WithSerializationContext(childContext2);
                            }
                            if (childCodec2 != null)
                            {
                                await childCodec2.DecodeFailureAsync(
                                    job.ResolveChildWorkflowExecutionStart.Cancelled.Failure).
                                    ConfigureAwait(false);
                            }
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveNexusOperation:
                        // TODO(cretz): Support Nexus serialization context
                        if (context.CodecNoContext == null)
                        {
                            break;
                        }
                        if (job.ResolveNexusOperation.Result.Completed != null)
                        {
                            await DecodeAsync(
                                context.CodecNoContext, job.ResolveNexusOperation.Result.Completed).
                                ConfigureAwait(false);
                        }
                        else if (job.ResolveNexusOperation.Result.Failed != null)
                        {
                            await context.CodecNoContext.DecodeFailureAsync(
                                job.ResolveNexusOperation.Result.Failed).
                                ConfigureAwait(false);
                        }
                        else if (job.ResolveNexusOperation.Result.Cancelled != null)
                        {
                            await context.CodecNoContext.DecodeFailureAsync(
                                job.ResolveNexusOperation.Result.Cancelled).
                                ConfigureAwait(false);
                        }
                        else if (job.ResolveNexusOperation.Result.TimedOut != null)
                        {
                            await context.CodecNoContext.DecodeFailureAsync(
                                job.ResolveNexusOperation.Result.TimedOut).
                                ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveNexusOperationStart:
                        // TODO(cretz): Support Nexus serialization context
                        if (job.ResolveNexusOperationStart.Failed != null
                            && context.CodecNoContext != null)
                        {
                            await context.CodecNoContext.DecodeFailureAsync(
                                job.ResolveNexusOperationStart.Failed).
                                ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveRequestCancelExternalWorkflow:
                        if (job.ResolveRequestCancelExternalWorkflow.Failure != null)
                        {
                            // Apply external workflow context
                            var extCanCodec = context.CodecNoContext;
                            if (extCanCodec is IWithSerializationContext<IPayloadCodec> withExtCan &&
                                context.Instance?.GetPendingExternalCancelSerializationContext(job.ResolveRequestCancelExternalWorkflow.Seq) is { } extCanContext)
                            {
                                extCanCodec = withExtCan.WithSerializationContext(extCanContext);
                            }
                            if (extCanCodec != null)
                            {
                                await extCanCodec.DecodeFailureAsync(
                                    job.ResolveRequestCancelExternalWorkflow.Failure).
                                    ConfigureAwait(false);
                            }
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveSignalExternalWorkflow:
                        if (job.ResolveSignalExternalWorkflow.Failure != null)
                        {
                            // Apply external workflow context
                            var extSigCodec = context.CodecNoContext;
                            if (extSigCodec is IWithSerializationContext<IPayloadCodec> withExtSig &&
                                context.Instance?.GetPendingExternalSignalSerializationContext(job.ResolveSignalExternalWorkflow.Seq) is { } extSigContext)
                            {
                                extSigCodec = withExtSig.WithSerializationContext(extSigContext);
                            }
                            if (extSigCodec != null)
                            {
                                await extSigCodec.DecodeFailureAsync(
                                    job.ResolveSignalExternalWorkflow.Failure).
                                    ConfigureAwait(false);
                            }
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.SignalWorkflow:
                        if (context.CodecWorkflowContext != null)
                        {
                            await DecodeAsync(context.CodecWorkflowContext, job.SignalWorkflow.Input).ConfigureAwait(false);
                            await DecodeAsync(context.CodecWorkflowContext, job.SignalWorkflow.Headers).ConfigureAwait(false);
                        }
                        break;
                    case WorkflowActivationJob.VariantOneofCase.InitializeWorkflow:
                        if (context.CodecWorkflowContext != null)
                        {
                            await DecodeAsync(context.CodecWorkflowContext, job.InitializeWorkflow).ConfigureAwait(false);
                        }
                        break;
                }
            }
        }

        private static async Task EncodeAsync(WorkflowCodecContext context, WorkflowCommand cmd)
        {
            // We capture the codec from this switch because user metadata needs it below
            IPayloadCodec? codec;
            switch (cmd.VariantCase)
            {
                case WorkflowCommand.VariantOneofCase.CompleteWorkflowExecution:
                    codec = context.CodecWorkflowContext;
                    if (cmd.CompleteWorkflowExecution.Result != null && codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.CompleteWorkflowExecution.Result).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ContinueAsNewWorkflowExecution:
                    codec = context.CodecWorkflowContext;
                    if (codec != null)
                    {
                        await EncodeAsync(
                                codec, cmd.ContinueAsNewWorkflowExecution.Arguments).ConfigureAwait(false);
                        await EncodeAsync(
                            codec, cmd.ContinueAsNewWorkflowExecution.Memo).ConfigureAwait(false);
                        await EncodeAsync(
                            codec, cmd.ContinueAsNewWorkflowExecution.Headers).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.FailWorkflowExecution:
                    codec = context.CodecWorkflowContext;
                    if (cmd.FailWorkflowExecution.Failure != null && codec != null)
                    {
                        await codec.EncodeFailureAsync(
                            cmd.FailWorkflowExecution.Failure).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ModifyWorkflowProperties:
                    codec = context.CodecWorkflowContext;
                    if (cmd.ModifyWorkflowProperties.UpsertedMemo != null && codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.ModifyWorkflowProperties.UpsertedMemo.Fields).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.RespondToQuery:
                    codec = context.CodecWorkflowContext;
                    if (cmd.RespondToQuery.Failed != null && codec != null)
                    {
                        await codec.EncodeFailureAsync(
                            cmd.RespondToQuery.Failed).ConfigureAwait(false);
                    }
                    else if (cmd.RespondToQuery.Succeeded?.Response != null && codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.RespondToQuery.Succeeded.Response).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleActivity:
                    // Apply activity context
                    codec = context.CodecNoContext;
                    if (codec is IWithSerializationContext<IPayloadCodec> withAct)
                    {
                        codec = withAct.WithSerializationContext(
                            new ISerializationContext.Activity(
                                Namespace: context.Namespace,
                                WorkflowId: context.WorkflowId,
                                WorkflowType: context.WorkflowType,
                                ActivityType: cmd.ScheduleActivity.ActivityType,
                                ActivityTaskQueue: cmd.ScheduleActivity.TaskQueue ?? context.TaskQueue,
                                IsLocal: false));
                    }
                    if (codec != null)
                    {
                        await EncodeAsync(codec, cmd.ScheduleActivity.Arguments).ConfigureAwait(false);
                        await EncodeAsync(codec, cmd.ScheduleActivity.Headers).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleLocalActivity:
                    // Apply activity context
                    codec = context.CodecNoContext;
                    if (codec is IWithSerializationContext<IPayloadCodec> withLocalAct)
                    {
                        codec = withLocalAct.WithSerializationContext(
                            new ISerializationContext.Activity(
                                Namespace: context.Namespace,
                                WorkflowId: context.WorkflowId,
                                WorkflowType: context.WorkflowType,
                                ActivityType: cmd.ScheduleLocalActivity.ActivityType,
                                ActivityTaskQueue: context.TaskQueue,
                                IsLocal: true));
                    }
                    if (codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.ScheduleLocalActivity.Arguments).ConfigureAwait(false);
                        await EncodeAsync(
                            codec, cmd.ScheduleLocalActivity.Headers).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.SignalExternalWorkflowExecution:
                    // Apply external workflow context
                    codec = context.CodecNoContext;
                    if (codec is IWithSerializationContext<IPayloadCodec> withExtSigContext)
                    {
                        var workflowId = cmd.SignalExternalWorkflowExecution.HasChildWorkflowId ?
                            cmd.SignalExternalWorkflowExecution.ChildWorkflowId :
                            cmd.SignalExternalWorkflowExecution.WorkflowExecution.WorkflowId;
                        codec = withExtSigContext.WithSerializationContext(
                            new ISerializationContext.Workflow(
                                Namespace: context.Namespace,
                                WorkflowId: workflowId));
                    }
                    if (codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.SignalExternalWorkflowExecution.Args).ConfigureAwait(false);
                        await EncodeAsync(
                            codec, cmd.SignalExternalWorkflowExecution.Headers).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleNexusOperation:
                    // TODO(cretz): Support Nexus serialization context
                    codec = context.CodecNoContext;
                    if (cmd.ScheduleNexusOperation.Input != null && codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.ScheduleNexusOperation.Input).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.StartChildWorkflowExecution:
                    // Apply child workflow context
                    codec = context.CodecNoContext;
                    if (codec is IWithSerializationContext<IPayloadCodec> withChild)
                    {
                        codec = withChild.WithSerializationContext(
                            new ISerializationContext.Workflow(
                                Namespace: context.Namespace,
                                WorkflowId: cmd.StartChildWorkflowExecution.WorkflowId));
                    }
                    if (codec != null)
                    {
                        await EncodeAsync(
                            codec, cmd.StartChildWorkflowExecution.Input).ConfigureAwait(false);
                        await EncodeAsync(
                            codec, cmd.StartChildWorkflowExecution.Memo).ConfigureAwait(false);
                        await EncodeAsync(
                            codec, cmd.StartChildWorkflowExecution.Headers).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.StartTimer:
                    codec = context.CodecWorkflowContext;
                    break;
                case WorkflowCommand.VariantOneofCase.UpdateResponse:
                    codec = context.CodecWorkflowContext;
                    if (cmd.UpdateResponse.Completed is { } updateCompleted && codec != null)
                    {
                        await EncodeAsync(codec, updateCompleted).ConfigureAwait(false);
                    }
                    else if (cmd.UpdateResponse.Rejected is { } updateRejected && codec != null)
                    {
                        await codec.EncodeFailureAsync(updateRejected).ConfigureAwait(false);
                    }
                    break;
                default:
                    codec = context.CodecWorkflowContext;
                    break;
            }
            if (cmd.UserMetadata != null && codec != null)
            {
                if (cmd.UserMetadata.Summary != null)
                {
                    await EncodeAsync(codec, cmd.UserMetadata.Summary).ConfigureAwait(false);
                }
                if (cmd.UserMetadata.Details != null)
                {
                    await EncodeAsync(codec, cmd.UserMetadata.Details).ConfigureAwait(false);
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
                    await EncodeAsync(codec, val).ConfigureAwait(false);
                }
            }
        }

        private static async Task EncodeAsync(
            IPayloadCodec codec, RepeatedField<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }
            // We have to convert to list here just in case they are based on the underlying list
            // and we clear it out (which can happen with Linq selectors)
            var newPayloads = (await codec.EncodeAsync(payloads).ConfigureAwait(false)).ToList();
            payloads.Clear();
            payloads.AddRange(newPayloads);
        }

        private static async Task EncodeAsync(IPayloadCodec codec, Payload payload)
        {
            // We are gonna require a single result here. It is important that we do Single() call
            // before clearing out payload to merge with since underlying enumerable may be lazy.
            // If the returned payload is literally the same object as the one sent to the codec,
            // we leave it alone.
            var encodedList = await codec.EncodeAsync(new Payload[] { payload }).ConfigureAwait(false);
            var encoded = encodedList.Single();
            if (!ReferenceEquals(encoded, payload))
            {
                payload.Metadata.Clear();
                payload.Data = ByteString.Empty;
                payload.MergeFrom(encoded);
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
                        await DecodeAsync(codec, res.Completed.Result).ConfigureAwait(false);
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
                        await DecodeAsync(codec, res.Completed.Result).ConfigureAwait(false);
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

        private static async Task DecodeAsync(IPayloadCodec codec, InitializeWorkflow init)
        {
            await DecodeAsync(codec, init.Arguments).ConfigureAwait(false);
            if (init.ContinuedFailure != null)
            {
                await codec.DecodeFailureAsync(init.ContinuedFailure).ConfigureAwait(false);
            }
            if (init.Memo != null)
            {
                await DecodeAsync(codec, init.Memo.Fields).ConfigureAwait(false);
            }
            await DecodeAsync(codec, init.Headers).ConfigureAwait(false);
            if (init.LastCompletionResult != null)
            {
                await DecodeAsync(codec, init.LastCompletionResult.Payloads_).ConfigureAwait(false);
            }
        }

        private static async Task DecodeAsync(
            IPayloadCodec codec, MapField<string, Payload> payloads)
        {
            foreach (var val in payloads.Values)
            {
                if (val != null)
                {
                    await DecodeAsync(codec, val).ConfigureAwait(false);
                }
            }
        }

        private static async Task DecodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }
            // We have to convert to list here just in case they are based on the underlying list
            // and we clear it out (which can happen with Linq selectors)
            var newPayloads = (await codec.DecodeAsync(payloads).ConfigureAwait(false)).ToList();
            payloads.Clear();
            payloads.AddRange(newPayloads);
        }

        private static async Task DecodeAsync(IPayloadCodec codec, Payload payload)
        {
            // We are gonna require a single result here.
            // Similarly with encode, we leave the payload alone if it's exactly the same object as the original.
            var decoded = await codec.DecodeAsync(new Payload[] { payload }).ConfigureAwait(false);
            var decodedPayload = decoded.Single();
            if (!ReferenceEquals(decodedPayload, payload))
            {
                payload.Metadata.Clear();
                payload.Data = ByteString.Empty;
                payload.MergeFrom(decodedPayload);
            }
        }

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
