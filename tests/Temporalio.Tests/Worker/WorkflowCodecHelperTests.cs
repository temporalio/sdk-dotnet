namespace Temporalio.Tests.Worker;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Converters;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class WorkflowCodecHelperTests : TestBase
{
    public WorkflowCodecHelperTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task CreateAndVisitPayload_Visiting_ReachesAllExpectedValues()
    {
        // This is just here to confirm our visitor works
        var paths = new List<string>();
        await CreateAndVisitPayload(
            new(),
            new WorkflowActivation(),
            (ctx, _) =>
            {
                paths.Add(ctx.Path);
                return Task.CompletedTask;
            });
        // Confirm some known paths are there for fixed field, repeated, and map
        Assert.Contains("Jobs.StartWorkflow.Headers", paths);
        Assert.Contains("Jobs.SignalWorkflow.Input", paths);
        Assert.Contains("Jobs.ResolveActivity.Result.Completed.Result", paths);
    }

    [Fact]
    public async Task EncodeAsync_AllPayloads_EncodesAll()
    {
        var comp = new WorkflowActivationCompletion();
        var codec = new MarkerPayloadCodec();
        await CreateAndVisitPayload(new(), comp, async (ctx, payload) =>
        {
            // We don't check search attributes on purpose
            if (ctx.PropertyPath.Last().Item2 == "SearchAttributes")
            {
                return;
            }
            Assert.DoesNotContain("encoded", payload().Metadata.Keys);
            await WorkflowCodecHelper.EncodeAsync(codec, comp);
            if (!payload().Metadata.ContainsKey("encoded"))
            {
                Assert.Fail($"Payload at path {ctx.Path} not encoded");
            }
        });
    }

    [Fact]
    public async Task DecodeAsync_AllPayloads_DecodesAll()
    {
        var act = new WorkflowActivation();
        var codec = new MarkerPayloadCodec();
        await CreateAndVisitPayload(new(), act, async (ctx, payload) =>
        {
            // We don't check search attributes on purpose
            if (ctx.PropertyPath.Any(t => t.Item2 == "SearchAttributes"))
            {
                return;
            }
            Assert.DoesNotContain("decoded", payload().Metadata.Keys);
            await WorkflowCodecHelper.DecodeAsync(codec, act);
            if (!payload().Metadata.ContainsKey("decoded"))
            {
                Assert.Fail($"Payload at path {ctx.Path} not decoded");
            }
        });
    }

    // Creates payloads as needed, null context if already seen
    private static async Task CreateAndVisitPayload(
        PayloadVisitContext ctx, IMessage current, Func<PayloadVisitContext, Func<Payload>, Task> visitor)
    {
        foreach (var prop in current.GetType().GetProperties())
        {
            if (prop.PropertyType.IsAssignableTo(typeof(Payload)))
            {
                var payload = new Payload();
                prop.SetValue(current, payload);
                await visitor(
                    ctx.WithProperty(current, prop.Name),
                    () => (Payload)prop.GetValue(current)!);
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(RepeatedField<Payload>)))
            {
                var payload = new Payload();
                ((RepeatedField<Payload>)prop.GetValue(current)!).Add(payload);
                await visitor(
                    ctx.WithProperty(current, prop.Name),
                    () => ((RepeatedField<Payload>)prop.GetValue(current)!)[0]);
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(MapField<string, Payload>)))
            {
                var payload = new Payload();
                ((MapField<string, Payload>)prop.GetValue(current)!)["some-key"] = payload;
                await visitor(
                    ctx.WithProperty(current, prop.Name),
                    () => ((MapField<string, Payload>)prop.GetValue(current)!)["some-key"]);
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(IMessage)))
            {
                if (!ctx.HasVisited(current.GetType(), prop.Name))
                {
                    var val = (IMessage)Activator.CreateInstance(prop.PropertyType)!;
                    prop.SetValue(current, val);
                    await CreateAndVisitPayload(ctx.WithProperty(current, prop.Name), val, visitor);
                }
            }
            else if (prop.PropertyType.Name == "RepeatedField`1" &&
                prop.PropertyType.GetGenericArguments().Length == 1 &&
                prop.PropertyType.GetGenericArguments()[0].IsAssignableTo(typeof(IMessage)))
            {
                if (!ctx.HasVisited(current.GetType(), prop.Name))
                {
                    var collVal = prop.GetValue(current)!;
                    var valType = prop.PropertyType.GetGenericArguments()[0];
                    var val = (IMessage)Activator.CreateInstance(valType)!;
                    collVal.GetType().GetMethod(
                        "Add", new[] { valType })!.Invoke(collVal, new[] { val });
                    await CreateAndVisitPayload(ctx.WithProperty(current, prop.Name), val, visitor);
                }
            }
        }
    }

    private record PayloadVisitContext(IEnumerable<Tuple<IMessage, string>> PropertyPath)
    {
        public PayloadVisitContext()
            : this(Enumerable.Empty<Tuple<IMessage, string>>())
        {
        }

        public string Path => string.Join('.', PropertyPath.Select(t => t.Item2));

        public bool HasVisited(Type messageType, string property) =>
            PropertyPath.Any(t => t.Item1.GetType() == messageType && t.Item2 == property);

        // Returns null if already seen
        public PayloadVisitContext WithProperty(IMessage current, string property) =>
            this with { PropertyPath = PropertyPath.Append(Tuple.Create(current, property)) };
    }

    private class MarkerPayloadCodec : IPayloadCodec
    {
        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var newP = p.Clone();
                newP.Metadata["encoded"] = ByteString.Empty;
                return newP;
            }).ToList());

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var newP = p.Clone();
                newP.Metadata["decoded"] = ByteString.Empty;
                return newP;
            }).ToList());
    }
}