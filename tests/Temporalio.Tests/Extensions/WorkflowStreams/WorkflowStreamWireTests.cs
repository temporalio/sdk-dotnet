namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System.Text.Json;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Extensions.WorkflowStreams;
using Xunit;

public class WorkflowStreamWireTests
{
    [Fact]
    public void PayloadWire_DefaultJsonString_MatchesCrossSdkFixture()
    {
        var payload = new Payload
        {
            Data = ByteString.CopyFromUtf8("\"hello\""),
        };
        payload.Metadata.Add("encoding", ByteString.CopyFromUtf8("json/plain"));

        const string Wire = "ChYKCGVuY29kaW5nEgpqc29uL3BsYWluEgciaGVsbG8i";
        Assert.Equal(Wire, PayloadWire.Encode(payload));

        var decoded = PayloadWire.Decode(Wire);
        Assert.Equal("json/plain", decoded.Metadata["encoding"].ToStringUtf8());
        Assert.Equal("\"hello\"", decoded.Data.ToStringUtf8());
    }

    [Fact]
    public void WireDtos_UseExactSnakeCaseNames()
    {
        var state = new WorkflowStreamState
        {
            Log = new[]
            {
                new WireItem { Topic = "orders", Data = "cGF5bG9hZA==", Offset = 3 },
            },
            BaseOffset = 3,
            PublisherSequences = new Dictionary<string, long> { ["publisher"] = 7 },
            PublisherLastSeen = new Dictionary<string, double> { ["publisher"] = 12.5 },
        };
        var publish = new PublishInput
        {
            Items = new[] { new PublishEntry { Topic = "orders", Data = "cGF5bG9hZA==" } },
            PublisherId = "publisher",
            Sequence = 7,
        };
        var poll = new PollInput { Topics = new[] { "orders" }, FromOffset = 3 };
        var result = new PollResult
        {
            Items = state.Log,
            NextOffset = 4,
            MoreReady = true,
        };

        AssertMore.JsonEqual(
            """
            {"items":[{"topic":"orders","data":"cGF5bG9hZA=="}],"publisher_id":"publisher","sequence":7}
            """,
            JsonSerializer.Serialize(publish));
        AssertMore.JsonEqual(
            """{"topics":["orders"],"from_offset":3}""",
            JsonSerializer.Serialize(poll));
        AssertMore.JsonEqual(
            """
            {"items":[{"topic":"orders","data":"cGF5bG9hZA==","offset":3}],"next_offset":4,"more_ready":true}
            """,
            JsonSerializer.Serialize(result));
        AssertMore.JsonEqual(
            """
            {"log":[{"topic":"orders","data":"cGF5bG9hZA==","offset":3}],"base_offset":3,"publisher_sequences":{"publisher":7},"publisher_last_seen":{"publisher":12.5}}
            """,
            JsonSerializer.Serialize(state));
    }

    [Fact]
    public void WireDtos_NormalizeNullTopics()
    {
        var entry = JsonSerializer.Deserialize<PublishEntry>("""{"topic":null,"data":""}""");
        var item = JsonSerializer.Deserialize<WireItem>(
            """{"topic":null,"data":"","offset":0}""");
        var poll = JsonSerializer.Deserialize<PollInput>(
            """{"topics":[null,"orders"],"from_offset":0}""");

        Assert.Equal(string.Empty, entry!.Topic);
        Assert.Equal(string.Empty, item!.Topic);
        Assert.Equal(new[] { string.Empty, "orders" }, poll!.Topics);
    }

    [Fact]
    public void Options_CloneSnapshotsTopicCollections()
    {
        var topics = new List<string> { "one" };
        var options = new WorkflowStreamSubscribeOptions { Topics = topics };
        var clone = (WorkflowStreamSubscribeOptions)options.Clone();
        topics.Add("two");

        Assert.Equal(new[] { "one" }, clone.Topics);
        Assert.NotSame(options.Topics, clone.Topics);
        options.Topics = new string[] { null! };
        Assert.Equal(new[] { string.Empty }, options.Topics);
        Assert.IsAssignableFrom<ICloneable>(new WorkflowStreamOptions());
        Assert.IsAssignableFrom<ICloneable>(new WorkflowStreamClientOptions());
    }
}
