namespace Temporalio.Tests.Worker;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Command.V1;
using Temporalio.Api.Common.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Bridge.Api.ActivityResult;
using Temporalio.Bridge.Api.ActivityTask;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class PayloadVisitorsTests : TestBase
{
    public PayloadVisitorsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    public static IEnumerable<object[]> CoreRoots() =>
        new List<object[]>
        {
            new object[] { new WorkflowActivation() },
            new object[] { new WorkflowActivationCompletion() },
            new object[] { new ActivityTask() },
            new object[] { new ActivityExecutionResult() },
        };

    [Theory]
    [MemberData(nameof(CoreRoots))]
    public async Task VisitAsync_EveryReachablePayload_IsVisitedUnlessSearchAttribute(IMessage root)
    {
        var checkedPaths = 0;
        var searchAttributePaths = 0;
        // One payload is populated at a time and visited immediately: sibling fields of the same
        // protobuf oneof clear each other, so only the payload just set is reachable. A visited
        // payload is detected by the marker the transform writes back onto it.
        await ForEachPayloadAsync(root, async (path, getPayload, underSearchAttributes, _) =>
        {
            await VisitAndMarkAsync(root);
            var wasVisited = WasVisited(getPayload());
            if (underSearchAttributes)
            {
                searchAttributePaths++;
                // The server indexes these literally, so transforming one makes it unsearchable.
                Assert.False(wasVisited, $"Search attribute at {path} was visited");
            }
            else
            {
                checkedPaths++;
                Assert.True(wasVisited, $"Payload at {path} was not visited");
            }
        });

        Assert.True(checkedPaths > 0, "No payload-bearing fields were reached at all");
        Assert.True(searchAttributePaths >= 0);
    }

    [Fact]
    public async Task VisitAsync_WorkflowActivation_ReachesKnownNestedPaths()
    {
        var paths = new List<string>();
        await ForEachPayloadAsync(
            new WorkflowActivation(),
            (path, _, _, _) =>
            {
                paths.Add(path);
                return Task.CompletedTask;
            });

        // A fixed field, a repeated field, a map, and a path several messages deep.
        Assert.Contains("Jobs.InitializeWorkflow.Headers", paths);
        Assert.Contains("Jobs.SignalWorkflow.Input", paths);
        Assert.Contains("Jobs.ResolveActivity.Result.Completed.Result", paths);
        Assert.Contains("Jobs.InitializeWorkflow.SearchAttributes.IndexedFields", paths);
    }

    [Fact]
    public async Task VisitAsync_MapOfPayloads_VisitsEachValueAsBatch()
    {
        var first = new Payload();
        var second = new Payload();
        var marker = new RecordMarkerCommandAttributes();
        marker.Details["some-key"] = new Payloads { Payloads_ = { first, second } };
        var batches = new List<IReadOnlyCollection<Payload>>();
        var singleCount = 0;

        await PayloadVisitors.VisitAsync(
            marker,
            PayloadVisitors.Walk,
            new PayloadVisitors.VisitOptions(
                (payload, _) =>
                {
                    singleCount++;
                    return Task.FromResult(payload);
                },
                (payloads, _) =>
                {
                    batches.Add(payloads.ToList());
                    return Task.FromResult(payloads);
                }));

        var batch = Assert.Single(batches);
        Assert.Equal(new[] { first, second }, batch);
        Assert.Equal(0, singleCount);
    }

    [Fact]
    public async Task VisitAsync_ApiRepeatedPayload_VisitsEachValueAlone()
    {
        var first = new Payload();
        var second = new Payload();
        var group = new CountWorkflowExecutionsResponse.Types.AggregationGroup
        {
            GroupValues = { first, second },
        };
        var singleCount = 0;
        var batchCount = 0;

        await PayloadVisitors.VisitAsync(
            group,
            PayloadVisitors.Walk,
            new PayloadVisitors.VisitOptions(
                (payload, _) =>
                {
                    singleCount++;
                    return Task.FromResult(payload);
                },
                (payloads, _) =>
                {
                    batchCount++;
                    return Task.FromResult(payloads);
                }));

        // Independent values are visited one at a time, never handed over as a batch.
        Assert.Equal(2, singleCount);
        Assert.Equal(0, batchCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task VisitAsync_MaxConcurrency_CapsInFlightTransforms(int maxConcurrency)
    {
        // Independent repeated payloads each become their own single-payload transform, so the walk
        // has several it may overlap. The transforms park on a gate to make the peak observable.
        var group = new CountWorkflowExecutionsResponse.Types.AggregationGroup();
        for (var i = 0; i < 10; i++)
        {
            group.GroupValues.Add(new Payload());
        }

        var running = 0;
        var peak = 0;
        var release = new TaskCompletionSource<bool>();

        var visit = PayloadVisitors.VisitAsync(
            group,
            PayloadVisitors.Walk,
            new PayloadVisitors.VisitOptions(
                async (payload, _) =>
                {
                    InterlockedMax(ref peak, Interlocked.Increment(ref running));
                    await release.Task;
                    Interlocked.Decrement(ref running);
                    return payload;
                },
                (payloads, _) => Task.FromResult(payloads))
            {
                MaxConcurrency = maxConcurrency,
            });

        // Let the admitted transforms pile up against the gate, then release them.
        await Task.Delay(50);
        Assert.Equal(maxConcurrency, Volatile.Read(ref running));
        release.SetResult(true);
        await visit;

        Assert.Equal(maxConcurrency, peak);
    }

    [Fact]
    public async Task VisitAsync_SiblingFailure_CancelsInFlightSiblings()
    {
        // Two independent payloads visited concurrently: one parks on its cancellation token, the
        // other throws once the first is in flight. The thrower's failure must cancel the token the
        // parked sibling is waiting on, rather than leaving it running.
        var waiter = new Payload { Metadata = { ["role"] = ByteString.CopyFromUtf8("wait") } };
        var thrower = new Payload { Metadata = { ["role"] = ByteString.CopyFromUtf8("throw") } };
        var group = new CountWorkflowExecutionsResponse.Types.AggregationGroup
        {
            GroupValues = { waiter, thrower },
        };
        var waiterInFlight = new TaskCompletionSource<bool>();
        var waiterCancelled = false;

        var visit = PayloadVisitors.VisitAsync(
            group,
            PayloadVisitors.Walk,
            new PayloadVisitors.VisitOptions(
                async (payload, cancellationToken) =>
                {
                    if (payload.Metadata["role"].ToStringUtf8() == "wait")
                    {
                        waiterInFlight.SetResult(true);
                        try
                        {
                            await Task.Delay(Timeout.Infinite, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            waiterCancelled = true;
                            throw;
                        }
                    }
                    else
                    {
                        await waiterInFlight.Task;
                        throw new InvalidOperationException("boom");
                    }

                    return payload;
                },
                (payloads, _) => Task.FromResult(payloads))
            {
                MaxConcurrency = 2,
            });

        // The real failure surfaces (not the sibling's cancellation), and the sibling was cancelled.
        var exc = await Assert.ThrowsAsync<InvalidOperationException>(() => visit);
        Assert.Equal("boom", exc.Message);
        Assert.True(waiterCancelled);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var seen = Interlocked.CompareExchange(ref target, value, current);
            if (seen == current)
            {
                return;
            }

            current = seen;
        }
    }

    private static async Task VisitAndMarkAsync(IMessage root)
    {
        var options = new PayloadVisitors.VisitOptions(
            (payload, _) => Task.FromResult(Mark(payload)),
            (payloads, _) => Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(Mark).ToList()));
        await (root switch
        {
            WorkflowActivation value => PayloadVisitors.VisitAsync(value, PayloadVisitors.Walk, options),
            WorkflowActivationCompletion value => PayloadVisitors.VisitAsync(value, PayloadVisitors.Walk, options),
            ActivityTask value => PayloadVisitors.VisitAsync(value, PayloadVisitors.Walk, options),
            ActivityExecutionResult value => PayloadVisitors.VisitAsync(value, PayloadVisitors.Walk, options),
            _ => throw new InvalidOperationException($"No entry point for {root.GetType()}"),
        });
    }

    // Returns a marked copy so the walker writes it back: a single payload's field is reassigned and
    // a batch field is rebuilt with the copies, which lets a test see what was reached.
    private static Payload Mark(Payload payload)
    {
        var marked = payload.Clone();
        marked.Metadata["__visited"] = ByteString.CopyFromUtf8("1");
        return marked;
    }

    private static bool WasVisited(Payload payload) => payload.Metadata.ContainsKey("__visited");

    private static Task ForEachPayloadAsync(
        IMessage root, Func<string, Func<Payload>, bool, bool, Task> onPayload) =>
        WalkAsync(root, string.Empty, new List<(Type Message, string Property)>(), false, onPayload);

    private static async Task WalkAsync(
        IMessage current,
        string path,
        List<(Type Message, string Property)> seen,
        bool underSearchAttributes,
        Func<string, Func<Payload>, bool, bool, Task> onPayload)
    {
        foreach (var prop in current.GetType().GetProperties())
        {
            var childPath = path.Length == 0 ? prop.Name : $"{path}.{prop.Name}";
            var inSearchAttributes = underSearchAttributes || prop.Name == "SearchAttributes";
            if (prop.PropertyType.IsAssignableTo(typeof(Payload)))
            {
                prop.SetValue(current, new Payload());
                await onPayload(
                    childPath, () => (Payload)prop.GetValue(current)!, inSearchAttributes, false);
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(RepeatedField<Payload>)))
            {
                var field = (RepeatedField<Payload>)prop.GetValue(current)!;
                field.Clear();
                field.Add(new Payload());
                await onPayload(
                    childPath,
                    () => ((RepeatedField<Payload>)prop.GetValue(current)!)[0],
                    inSearchAttributes,
                    true);
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(MapField<string, Payload>)))
            {
                var field = (MapField<string, Payload>)prop.GetValue(current)!;
                field["some-key"] = new Payload();
                await onPayload(
                    childPath,
                    () => ((MapField<string, Payload>)prop.GetValue(current)!)["some-key"],
                    inSearchAttributes,
                    false);
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(IMessage)))
            {
                if (seen.Contains((current.GetType(), prop.Name)))
                {
                    continue;
                }

                var value = (IMessage)Activator.CreateInstance(prop.PropertyType)!;
                prop.SetValue(current, value);
                await WalkAsync(
                    value,
                    childPath,
                    Append(seen, current, prop.Name),
                    inSearchAttributes,
                    onPayload);
            }
            else if (prop.PropertyType.Name == "RepeatedField`1" &&
                prop.PropertyType.GetGenericArguments()[0].IsAssignableTo(typeof(IMessage)))
            {
                if (seen.Contains((current.GetType(), prop.Name)))
                {
                    continue;
                }

                var collection = prop.GetValue(current)!;
                var valueType = prop.PropertyType.GetGenericArguments()[0];
                var value = (IMessage)Activator.CreateInstance(valueType)!;
                collection.GetType().GetMethod("Add", new[] { valueType })!
                    .Invoke(collection, new[] { (object)value });
                await WalkAsync(
                    value,
                    childPath,
                    Append(seen, current, prop.Name),
                    inSearchAttributes,
                    onPayload);
            }
        }
    }

    private static List<(Type Message, string Property)> Append(
        List<(Type Message, string Property)> seen, IMessage current, string property) =>
        new(seen) { (current.GetType(), property) };
}
