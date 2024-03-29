using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Client
{
    public partial class TemporalClient
    {
        /// <inheritdoc />
        public AsyncActivityHandle GetAsyncActivityHandle(byte[] taskToken) =>
            new(this, new AsyncActivityHandle.TaskTokenReference(taskToken));

        /// <inheritdoc />
        public AsyncActivityHandle GetAsyncActivityHandle(
            string workflowId, string runId, string activityId) =>
            new(this, new AsyncActivityHandle.IdReference(
                WorkflowId: workflowId, RunId: runId, ActivityId: activityId));

        internal partial class Impl
        {
            /// <inheritdoc />
            public override async Task HeartbeatAsyncActivityAsync(HeartbeatAsyncActivityInput input)
            {
                Payloads? details = null;
                if (input.Options?.Details != null && input.Options.Details.Count > 0)
                {
                    details = new()
                    {
                        Payloads_ =
                        {
                            await Client.Options.DataConverter.ToPayloadsAsync(
                                input.Options.Details).ConfigureAwait(false),
                        },
                    };
                }
                if (input.Activity is AsyncActivityHandle.IdReference idRef)
                {
                    var resp = await Client.Connection.WorkflowService.RecordActivityTaskHeartbeatByIdAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            WorkflowId = idRef.WorkflowId,
                            RunId = idRef.RunId ?? string.Empty,
                            ActivityId = idRef.ActivityId,
                            Details = details,
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                    if (resp.CancelRequested)
                    {
                        throw new AsyncActivityCanceledException();
                    }
                }
                else if (input.Activity is AsyncActivityHandle.TaskTokenReference tokRef)
                {
                    var resp = await Client.Connection.WorkflowService.RecordActivityTaskHeartbeatAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            TaskToken = ByteString.CopyFrom(tokRef.TaskToken),
                            Details = details,
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                    if (resp.CancelRequested)
                    {
                        throw new AsyncActivityCanceledException();
                    }
                }
                else
                {
                    throw new ArgumentException("Unrecognized activity reference type");
                }
            }

            /// <inheritdoc />
            public override async Task CompleteAsyncActivityAsync(CompleteAsyncActivityInput input)
            {
                var result = await Client.Options.DataConverter.ToPayloadAsync(
                    input.Result).ConfigureAwait(false);
                if (input.Activity is AsyncActivityHandle.IdReference idRef)
                {
                    await Client.Connection.WorkflowService.RespondActivityTaskCompletedByIdAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            WorkflowId = idRef.WorkflowId,
                            RunId = idRef.RunId ?? string.Empty,
                            ActivityId = idRef.ActivityId,
                            Result = new() { Payloads_ = { { result } } },
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                else if (input.Activity is AsyncActivityHandle.TaskTokenReference tokRef)
                {
                    await Client.Connection.WorkflowService.RespondActivityTaskCompletedAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            TaskToken = ByteString.CopyFrom(tokRef.TaskToken),
                            Result = new() { Payloads_ = { { result } } },
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("Unrecognized activity reference type");
                }
            }

            /// <inheritdoc />
            public override async Task FailAsyncActivityAsync(FailAsyncActivityInput input)
            {
                var failure = await Client.Options.DataConverter.ToFailureAsync(
                    input.Exception).ConfigureAwait(false);
                Payloads? lastHeartbeatDetails = null;
                if (input.Options?.LastHeartbeatDetails != null &&
                    input.Options.LastHeartbeatDetails.Count > 0)
                {
                    lastHeartbeatDetails = new()
                    {
                        Payloads_ =
                        {
                            await Client.Options.DataConverter.ToPayloadsAsync(
                                input.Options.LastHeartbeatDetails).ConfigureAwait(false),
                        },
                    };
                }
                if (input.Activity is AsyncActivityHandle.IdReference idRef)
                {
                    await Client.Connection.WorkflowService.RespondActivityTaskFailedByIdAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            WorkflowId = idRef.WorkflowId,
                            RunId = idRef.RunId ?? string.Empty,
                            ActivityId = idRef.ActivityId,
                            Failure = failure,
                            LastHeartbeatDetails = lastHeartbeatDetails,
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                else if (input.Activity is AsyncActivityHandle.TaskTokenReference tokRef)
                {
                    await Client.Connection.WorkflowService.RespondActivityTaskFailedAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            TaskToken = ByteString.CopyFrom(tokRef.TaskToken),
                            Failure = failure,
                            LastHeartbeatDetails = lastHeartbeatDetails,
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("Unrecognized activity reference type");
                }
            }

            /// <inheritdoc />
            public override async Task ReportCancellationAsyncActivityAsync(
                ReportCancellationAsyncActivityInput input)
            {
                Payloads? details = null;
                if (input.Options?.Details != null && input.Options.Details.Count > 0)
                {
                    details = new()
                    {
                        Payloads_ =
                        {
                            await Client.Options.DataConverter.ToPayloadsAsync(
                                input.Options.Details).ConfigureAwait(false),
                        },
                    };
                }
                if (input.Activity is AsyncActivityHandle.IdReference idRef)
                {
                    await Client.Connection.WorkflowService.RespondActivityTaskCanceledByIdAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            WorkflowId = idRef.WorkflowId,
                            RunId = idRef.RunId ?? string.Empty,
                            ActivityId = idRef.ActivityId,
                            Details = details,
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                else if (input.Activity is AsyncActivityHandle.TaskTokenReference tokRef)
                {
                    await Client.Connection.WorkflowService.RespondActivityTaskCanceledAsync(
                        new()
                        {
                            Namespace = Client.Options.Namespace,
                            Identity = Client.Connection.Options.Identity,
                            TaskToken = ByteString.CopyFrom(tokRef.TaskToken),
                            Details = details,
                        },
                        DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("Unrecognized activity reference type");
                }
            }
        }
    }
}