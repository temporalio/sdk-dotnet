namespace Temporalio.Tests.Worker;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Command.V1;
using Temporalio.Api.Common.V1;
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
        // protobuf oneof clear each other, so only the payload just set is reachable.
        await ForEachPayloadAsync(root, async (path, getPayload, underSearchAttributes, repeated) =>
        {
            var (visited, batched) = await VisitAsync(root);
            var wasVisited = visited.Any(p => ReferenceEquals(p, getPayload()));
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
                if (repeated)
                {
                    // Codecs that pack or split payloads only work when handed the whole field.
                    Assert.True(
                        batched.Any(p => ReferenceEquals(p, getPayload())),
                        $"Repeated payload at {path} was not visited as a batch");
                }
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

        await PayloadVisitors.VisitAsync(
            marker,
            _ => throw new InvalidOperationException("Payload visited outside its batch"),
            payloads =>
            {
                batches.Add(payloads.ToList());
                return Task.FromResult(payloads);
            });

        var batch = Assert.Single(batches);
        Assert.Equal(new[] { first, second }, batch);
    }

    private static async Task<(List<Payload> Visited, List<Payload> Batched)> VisitAsync(
        IMessage root)
    {
        var visited = new List<Payload>();
        var batched = new List<Payload>();
        PayloadVisitors.PayloadVisitor visitPayload = payload =>
        {
            visited.Add(payload);
            return Task.CompletedTask;
        };
        PayloadVisitors.PayloadsVisitor visitPayloads = payloads =>
        {
            visited.AddRange(payloads);
            batched.AddRange(payloads);
            return Task.FromResult(payloads);
        };
        await (root switch
        {
            WorkflowActivation value => PayloadVisitors.VisitAsync(value, visitPayload, visitPayloads),
            WorkflowActivationCompletion value => PayloadVisitors.VisitAsync(value, visitPayload, visitPayloads),
            ActivityTask value => PayloadVisitors.VisitAsync(value, visitPayload, visitPayloads),
            ActivityExecutionResult value => PayloadVisitors.VisitAsync(value, visitPayload, visitPayloads),
            _ => throw new InvalidOperationException($"No entry point for {root.GetType()}"),
        });
        return (visited, batched);
    }

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
