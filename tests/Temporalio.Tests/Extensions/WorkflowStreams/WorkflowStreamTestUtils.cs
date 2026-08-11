namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Extensions.WorkflowStreams.Internal;

internal static class WorkflowStreamTestUtils
{
    public static PublishInput PublishInputFor(string publisherId, long seq, params string[] topicValues)
    {
        var items = new List<PublishEntry>();
        for (var i = 0; i < topicValues.Length; i += 2)
        {
            var payload = DataConverter.Default.PayloadConverter.ToPayload(topicValues[i + 1]);
            items.Add(new PublishEntry { Topic = topicValues[i], Data = PayloadWire.Encode(payload) });
        }
        return new PublishInput { Items = items, PublisherId = publisherId, Sequence = seq };
    }

    public static string Decode(WorkflowStreamItem item) =>
        DataConverter.Default.PayloadConverter.ToValue<string>(item.Payload);

    public static string Decode(WireItem item) =>
        DataConverter.Default.PayloadConverter.ToValue<string>(PayloadWire.Decode(item.Data!));

    // A successful offset query proves the workflow initialized and the stream registered its
    // handlers. Before that, publish signals may sit buffered behind attribute-registered
    // signals or updates that arrived in the same first workflow task, which would reorder
    // them (pre-init, only handlers known statically are dispatchable).
    public static async Task WaitStreamReadyAsync(WorkflowHandle handle)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                await handle.QueryAsync<long>(
                    WorkflowStreamConstants.OffsetQueryName, Array.Empty<object?>());
                return;
            }
#pragma warning disable CA1031 // Retrying on any failure until the workflow initializes
            catch (Exception)
#pragma warning restore CA1031
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw;
                }
                await Task.Delay(50);
            }
        }
    }
}
