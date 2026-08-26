namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Xunit;

// Locks the JSON contract of the wire protocol envelope types: the snake_case field names are
// the cross-language contract shared with the other SDKs' workflow streams packages.
public class WireProtocolTests
{
    [Fact]
    public void PublishInput_SerializesWithWireFieldNames()
    {
        var json = SerializeToJson(new PublishInput
        {
            Items = { new PublishEntry { Topic = "t", Data = "d" } },
            PublisherId = "pub",
            Sequence = 3,
        });

        Assert.Equal(
            new[] { "items", "publisher_id", "sequence" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal("pub", json.GetProperty("publisher_id").GetString());
        Assert.Equal(3, json.GetProperty("sequence").GetInt64());
        Assert.Equal(
            new[] { "data", "topic" },
            json.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void PollInput_SerializesWithWireFieldNames()
    {
        var json = SerializeToJson(new PollInput { Topics = { "a" }, FromOffset = 7 });

        Assert.Equal(
            new[] { "from_offset", "topics" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(7, json.GetProperty("from_offset").GetInt64());
    }

    [Fact]
    public void PollResult_SerializesWithWireFieldNames()
    {
        var json = SerializeToJson(new PollResult
        {
            Items = { new WireItem { Topic = "t", Data = "d", Offset = 5 } },
            NextOffset = 6,
            MoreReady = true,
        });

        Assert.Equal(
            new[] { "items", "more_ready", "next_offset" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(6, json.GetProperty("next_offset").GetInt64());
        Assert.True(json.GetProperty("more_ready").GetBoolean());
        Assert.Equal(
            new[] { "data", "offset", "topic" },
            json.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(5, json.GetProperty("items")[0].GetProperty("offset").GetInt64());
    }

    [Fact]
    public void WorkflowStreamState_SerializesWithWireFieldNames()
    {
        var json = SerializeToJson(new WorkflowStreamState
        {
            Log = { new WireItem { Topic = "t", Data = "d", Offset = 0 } },
            BaseOffset = 4,
            PublisherSequences = new Dictionary<string, long> { ["pub"] = 2 },
            PublisherLastSeen = new Dictionary<string, double> { ["pub"] = 123.5 },
        });

        Assert.Equal(
            new[] { "base_offset", "log", "publisher_last_seen", "publisher_sequences" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(4, json.GetProperty("base_offset").GetInt64());
        Assert.Equal(2, json.GetProperty("publisher_sequences").GetProperty("pub").GetInt64());
        Assert.Equal(123.5, json.GetProperty("publisher_last_seen").GetProperty("pub").GetDouble());
    }

    [Fact]
    public void Envelopes_RoundTripThroughDataConverter()
    {
        var input = new PublishInput
        {
            Items = { new PublishEntry { Topic = "t", Data = "d" } },
            PublisherId = "pub",
            Sequence = 3,
        };
        var payload = DataConverter.Default.PayloadConverter.ToPayload(input);
        var decoded = DataConverter.Default.PayloadConverter.ToValue<PublishInput>(payload);

        Assert.Equal("pub", decoded.PublisherId);
        Assert.Equal(3, decoded.Sequence);
        Assert.Single(decoded.Items);
        Assert.Equal("t", decoded.Items[0].Topic);
        Assert.Equal("d", decoded.Items[0].Data);
    }

    [Fact]
    public void WireTopics_NormalizeNullToEmpty()
    {
        var publishEntry = DataConverter.Default.PayloadConverter.ToValue<PublishEntry>(
            DataConverter.Default.PayloadConverter.ToPayload(new { topic = (string?)null, data = "d" }));
        var wireItem = DataConverter.Default.PayloadConverter.ToValue<WireItem>(
            DataConverter.Default.PayloadConverter.ToPayload(
                new { topic = (string?)null, data = "d", offset = 1 }));

        Assert.Equal(string.Empty, publishEntry.Topic);
        Assert.Equal(string.Empty, wireItem.Topic);
    }

    private static JsonElement SerializeToJson(object dto)
    {
        var payload = DataConverter.Default.PayloadConverter.ToPayload(dto);
        return JsonDocument.Parse(payload.Data.ToStringUtf8()).RootElement;
    }
}
