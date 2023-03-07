using System.Linq;
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
        /// <param name="codec">Codec to use.</param>
        /// <param name="comp">Completion to encode.</param>
        /// <returns>Task for completion.</returns>
        internal static async Task EncodeAsync(
            IPayloadCodec codec, WorkflowActivationCompletion comp)
        {
            switch (comp.StatusCase)
            {
                case WorkflowActivationCompletion.StatusOneofCase.Failed:
                    if (comp.Failed.Failure_ != null)
                    {
                        await codec.EncodeFailureAsync(comp.Failed.Failure_).ConfigureAwait(false);
                    }
                    break;
                case WorkflowActivationCompletion.StatusOneofCase.Successful:
                    foreach (var cmd in comp.Successful.Commands)
                    {
                        await EncodeAsync(codec, cmd).ConfigureAwait(false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Decode the activation.
        /// </summary>
        /// <param name="codec">Codec to use.</param>
        /// <param name="act">Activation to decode.</param>
        /// <returns>Task for completion.</returns>
        internal static async Task DecodeAsync(IPayloadCodec codec, WorkflowActivation act)
        {
            foreach (var job in act.Jobs)
            {
                switch (job.VariantCase)
                {
                    case WorkflowActivationJob.VariantOneofCase.CancelWorkflow:
                        await DecodeAsync(codec, job.CancelWorkflow.Details).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.QueryWorkflow:
                        await DecodeAsync(codec, job.QueryWorkflow.Arguments).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveActivity:
                        await DecodeAsync(codec, job.ResolveActivity.Result).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecution:
                        await DecodeAsync(
                            codec, job.ResolveChildWorkflowExecution.Result).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecutionStart:
                        if (job.ResolveChildWorkflowExecutionStart.Cancelled != null
                            && job.ResolveChildWorkflowExecutionStart.Cancelled.Failure != null)
                        {
                            await codec.DecodeFailureAsync(
                                job.ResolveChildWorkflowExecutionStart.Cancelled.Failure).
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
                        await DecodeAsync(codec, job.SignalWorkflow.Input).ConfigureAwait(false);
                        break;
                    case WorkflowActivationJob.VariantOneofCase.StartWorkflow:
                        await DecodeAsync(codec, job.StartWorkflow).ConfigureAwait(false);
                        break;
                }
            }
        }

        private static async Task EncodeAsync(IPayloadCodec codec, WorkflowCommand cmd)
        {
            switch (cmd.VariantCase)
            {
                case WorkflowCommand.VariantOneofCase.CompleteWorkflowExecution:
                    if (cmd.CompleteWorkflowExecution.Result != null)
                    {
                        await EncodeAsync(
                            codec, cmd.CompleteWorkflowExecution.Result).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ContinueAsNewWorkflowExecution:
                    await EncodeAsync(
                        codec, cmd.ContinueAsNewWorkflowExecution.Arguments).ConfigureAwait(false);
                    foreach (var val in cmd.ContinueAsNewWorkflowExecution.Memo.Values)
                    {
                        if (val != null)
                        {
                            await EncodeAsync(codec, val).ConfigureAwait(false);
                        }
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.FailWorkflowExecution:
                    if (cmd.FailWorkflowExecution.Failure != null)
                    {
                        await codec.EncodeFailureAsync(
                            cmd.FailWorkflowExecution.Failure).ConfigureAwait(false);
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
                        await EncodeAsync(
                            codec, cmd.RespondToQuery.Succeeded.Response).ConfigureAwait(false);
                    }
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleActivity:
                    await EncodeAsync(codec, cmd.ScheduleActivity.Arguments).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.ScheduleLocalActivity:
                    await EncodeAsync(
                        codec, cmd.ScheduleLocalActivity.Arguments).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.SignalExternalWorkflowExecution:
                    await EncodeAsync(
                        codec, cmd.SignalExternalWorkflowExecution.Args).ConfigureAwait(false);
                    break;
                case WorkflowCommand.VariantOneofCase.StartChildWorkflowExecution:
                    await EncodeAsync(
                        codec, cmd.StartChildWorkflowExecution.Input).ConfigureAwait(false);
                    foreach (var val in cmd.StartChildWorkflowExecution.Memo.Values)
                    {
                        if (val != null)
                        {
                            await EncodeAsync(codec, val).ConfigureAwait(false);
                        }
                    }
                    break;
            }
        }

        private static async Task EncodeAsync(
            IPayloadCodec codec, RepeatedField<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }
            var newPayloads = await codec.EncodeAsync(payloads).ConfigureAwait(false);
            payloads.Clear();
            payloads.AddRange(newPayloads);
        }

        private static async Task EncodeAsync(IPayloadCodec codec, Payload payload)
        {
            // We are gonna require a single result here
            var encoded = await codec.EncodeAsync(new Payload[] { payload }).ConfigureAwait(false);
            payload.Metadata.Clear();
            payload.MergeFrom(encoded.Single());
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

        private static async Task DecodeAsync(IPayloadCodec codec, StartWorkflow start)
        {
            await DecodeAsync(codec, start.Arguments).ConfigureAwait(false);
            if (start.ContinuedFailure != null)
            {
                await codec.DecodeFailureAsync(start.ContinuedFailure).ConfigureAwait(false);
            }
            if (start.Memo != null)
            {
                foreach (var val in start.Memo.Fields.Values)
                {
                    if (val != null)
                    {
                        await DecodeAsync(codec, val).ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task DecodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }
            var newPayloads = await codec.DecodeAsync(payloads).ConfigureAwait(false);
            payloads.Clear();
            payloads.AddRange(newPayloads);
        }

        private static async Task DecodeAsync(IPayloadCodec codec, Payload payload)
        {
            // We are gonna require a single result here
            var decoded = await codec.DecodeAsync(new Payload[] { payload }).ConfigureAwait(false);
            payload.Metadata.Clear();
            payload.MergeFrom(decoded.Single());
        }
    }
}