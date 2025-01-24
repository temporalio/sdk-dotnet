using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client.Interceptors;
using Temporalio.Client.Schedules;
using Temporalio.Converters;
using Temporalio.Exceptions;

#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Threading;
#endif

namespace Temporalio.Client
{
    public partial class TemporalClient
    {
        /// <inheritdoc />
        public Task<ScheduleHandle> CreateScheduleAsync(
            string scheduleId, Schedule schedule, ScheduleOptions? options = null) =>
            OutboundInterceptor.CreateScheduleAsync(new(
                Id: scheduleId,
                Schedule: schedule,
                Options: options));

        /// <inheritdoc />
        public ScheduleHandle GetScheduleHandle(string scheduleId) => new(this, scheduleId);

#if NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc />
        public IAsyncEnumerable<ScheduleListDescription> ListSchedulesAsync(
            ScheduleListOptions? options = null) =>
            OutboundInterceptor.ListSchedulesAsync(new(options));
#endif

        internal partial class Impl
        {
            /// <inheritdoc />
            public override async Task<ScheduleHandle> CreateScheduleAsync(CreateScheduleInput input)
            {
                var req = new CreateScheduleRequest()
                {
                    Namespace = Client.Options.Namespace,
                    ScheduleId = input.Id,
                    Schedule = await input.Schedule.ToProtoAsync(Client.Options.DataConverter).ConfigureAwait(false),
                    Identity = Client.Connection.Options.Identity,
                    RequestId = Guid.NewGuid().ToString(),
                };
                if (input.Options?.Memo != null && input.Options.Memo.Count > 0)
                {
                    req.Memo = new();
                    foreach (var field in input.Options.Memo)
                    {
                        if (field.Value == null)
                        {
                            throw new ArgumentException($"Memo value for {field.Key} is null");
                        }
                        req.Memo.Fields.Add(
                            field.Key,
                            await Client.Options.DataConverter.ToPayloadAsync(field.Value).ConfigureAwait(false));
                    }
                }
                if (input.Options?.TypedSearchAttributes != null && input.Options.TypedSearchAttributes.Count > 0)
                {
                    req.SearchAttributes = input.Options.TypedSearchAttributes.ToProto();
                }
                if (input.Options?.TriggerImmediately == true)
                {
                    req.InitialPatch = new()
                    {
                        TriggerImmediately = new()
                        {
                            OverlapPolicy = input.Schedule.Policy.Overlap,
                        },
                    };
                }
                if (input.Options?.Backfills is IReadOnlyCollection<ScheduleBackfill> backfills)
                {
                    if (req.InitialPatch == null)
                    {
                        req.InitialPatch = new();
                    }
                    req.InitialPatch.BackfillRequest.Add(backfills.Select(b => b.ToProto()));
                }

                try
                {
                    await Client.Connection.WorkflowService.CreateScheduleAsync(
                        req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                    return new(Client, input.Id);
                }
                catch (RpcException e) when (e.Code == RpcException.StatusCode.AlreadyExists)
                {
                    throw new ScheduleAlreadyRunningException();
                }
            }

            /// <inheritdoc />
            public override Task BackfillScheduleAsync(BackfillScheduleInput input) =>
                Client.Connection.WorkflowService.PatchScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                        Patch = new()
                        {
                            BackfillRequest = { input.Backfills.Select(b => b.ToProto()) },
                        },
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                    },
                    DefaultRetryOptions(input.RpcOptions));

            /// <inheritdoc />
            public override Task DeleteScheduleAsync(DeleteScheduleInput input) =>
                Client.Connection.WorkflowService.DeleteScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                        Identity = Client.Connection.Options.Identity,
                    },
                    DefaultRetryOptions(input.RpcOptions));

            /// <inheritdoc />
            public override async Task<ScheduleDescription> DescribeScheduleAsync(
                DescribeScheduleInput input)
            {
                var desc = await Client.Connection.WorkflowService.DescribeScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                    },
                    DefaultRetryOptions(input.RpcOptions)).ConfigureAwait(false);
                return await ScheduleDescription.FromProtoAsync(
                    input.Id, desc, Client.Options.DataConverter).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override Task PauseScheduleAsync(PauseScheduleInput input) =>
                Client.Connection.WorkflowService.PatchScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                        Patch = new() { Pause = input.Note ?? "Paused via .NET SDK" },
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                    },
                    DefaultRetryOptions(input.RpcOptions));

            /// <inheritdoc />
            public override Task TriggerScheduleAsync(TriggerScheduleInput input) =>
                Client.Connection.WorkflowService.PatchScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                        Patch = new()
                        {
                            TriggerImmediately = new()
                            {
                                OverlapPolicy = input.Options?.Overlap ?? ScheduleOverlapPolicy.Unspecified,
                            },
                        },
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                    },
                    DefaultRetryOptions(input.Options?.Rpc));

            /// <inheritdoc />
            public override Task UnpauseScheduleAsync(UnpauseScheduleInput input) =>
                Client.Connection.WorkflowService.PatchScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                        Patch = new() { Unpause = input.Note ?? "Unpaused via .NET SDK" },
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                    },
                    DefaultRetryOptions(input.RpcOptions));

            /// <inheritdoc />
            public override async Task UpdateScheduleAsync(UpdateScheduleInput input)
            {
                // TODO(cretz): This is supposed to be a retry-conflict loop, but we do not yet have
                // a way to know update failure is due to conflict token mismatch
                var desc = await DescribeScheduleAsync(new(input.Id, input.RpcOptions)).ConfigureAwait(false);
                var update = await input.Updater(new(Description: desc)).ConfigureAwait(false);
                if (update == null)
                {
                    return;
                }
                await Client.Connection.WorkflowService.UpdateScheduleAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ScheduleId = input.Id,
                        Schedule = await update.Schedule.ToProtoAsync(Client.Options.DataConverter).ConfigureAwait(false),
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        SearchAttributes = update.TypedSearchAttributes?.ToProto(),
                    },
                    DefaultRetryOptions(input.RpcOptions)).ConfigureAwait(false);
            }

#if NETCOREAPP3_0_OR_GREATER
            /// <inheritdoc />
            public override IAsyncEnumerable<ScheduleListDescription> ListSchedulesAsync(
                ListSchedulesInput input) => ListSchedulesInternalAsync(input);

            private async IAsyncEnumerable<ScheduleListDescription> ListSchedulesInternalAsync(
                ListSchedulesInput input,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                // Need to combine cancellation token
                var rpcOptsAndCancelSource = DefaultRetryOptions(input.Options?.Rpc).
                    WithAdditionalCancellationToken(cancellationToken);
                try
                {
                    var req = new ListSchedulesRequest
                    {
                        // TODO(cretz): Allow setting of page size or next page token?
                        Namespace = Client.Options.Namespace,
                        Query = input.Options?.Query ?? string.Empty,
                    };
                    do
                    {
                        var resp = await Client.Connection.WorkflowService.ListSchedulesAsync(
                            req, rpcOptsAndCancelSource.Item1).ConfigureAwait(false);
                        foreach (var entry in resp.Schedules)
                        {
                            yield return new(entry, Client.Options.DataConverter);
                        }
                        req.NextPageToken = resp.NextPageToken;
                    }
                    while (!req.NextPageToken.IsEmpty);
                }
                finally
                {
                    rpcOptsAndCancelSource.Item2?.Dispose();
                }
            }
#endif
        }
    }
}