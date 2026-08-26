namespace Temporalio.Tests.Worker;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCommands;
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
        Assert.Contains("Jobs.InitializeWorkflow.Headers", paths);
        Assert.Contains("Jobs.SignalWorkflow.Input", paths);
        Assert.Contains("Jobs.ResolveActivity.Result.Completed.Result", paths);
    }

    [Fact]
    public async Task EncodeAsync_AllPayloads_EncodesAll()
    {
        var comp = new WorkflowActivationCompletion();
        var codecs = new List<IPayloadCodec> { new MarkerPayloadCodec(), new MarkerNoClonePayloadCodec() };
        await CreateAndVisitPayload(new(), comp, async (ctx, payload) =>
        {
            // We don't check search attributes on purpose
            if (ctx.PropertyPath.Any(t => t.Item2 == "SearchAttributes"))
            {
                return;
            }
            Assert.DoesNotContain("encoded", payload().Metadata.Keys);
            foreach (var codec in codecs)
            {
                await WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(codec), comp);
                if (!payload().Metadata.ContainsKey("encoded"))
                {
                    Assert.Fail($"Payload at path {ctx.Path} not encoded with codec {codec}");
                }
            }
        });
    }

    [Fact]
    public async Task DecodeAsync_AllPayloads_DecodesAll()
    {
        var act = new WorkflowActivation();
        var codecs = new List<IPayloadCodec> { new MarkerPayloadCodec(), new MarkerNoClonePayloadCodec() };
        await CreateAndVisitPayload(new(), act, async (ctx, payload) =>
        {
            // We don't check search attributes on purpose
            if (ctx.PropertyPath.Any(t => t.Item2 == "SearchAttributes"))
            {
                return;
            }
            Assert.DoesNotContain("decoded", payload().Metadata.Keys);
            foreach (var codec in codecs)
            {
                await WorkflowCodecHelper.DecodeAsync(CreateSimpleCodecContext(codec), act);
                if (!payload().Metadata.ContainsKey("decoded"))
                {
                    Assert.Fail($"Payload at path {ctx.Path} not decoded with codec {codec}");
                }
            }
        });
    }

    [Fact]
    public async Task EncodeAsync_AllPayloads_WorksWithNull()
    {
        // For every singular Payload field, we are going to set it to null and ensure it can still
        // encode. This is to prevent regression since we missed that sometimes we are not checking
        // for null in WorkflowCodecHelper.
        var comp = new WorkflowActivationCompletion();
        var codec = new MarkerPayloadCodec();
        await CreateAndVisitPayload(new(), comp, async (ctx, payload) =>
        {
            var (msg, prop) = ctx.PropertyPath.Last();
            var propInfo = msg.GetType().GetProperty(prop);
            if (propInfo?.PropertyType == typeof(Payload))
            {
                propInfo.SetValue(msg, null);
                await WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(codec), comp);
            }
        });
    }

    [Fact]
    public async Task EncodeAsync_SystemNexusEnvelopeInGenericPayloadField_EncodesNestedPayload()
    {
        var request = new SignalWithStartWorkflowExecutionRequest
        {
            Input = new() { Payloads_ = { new Payload { Data = ByteString.CopyFromUtf8("input") } } },
        };
        var envelope = CreateSystemEnvelope(request);
        var completion = new WorkflowActivationCompletion
        {
            Successful = new()
            {
                Commands =
                {
                    new WorkflowCommand { UpdateResponse = new() { Completed = envelope } },
                },
            },
        };

        await WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(new MarkerPayloadCodec()), completion);

        Assert.Equal(
            ByteString.CopyFromUtf8("true"),
            completion.Successful.Commands[0].UpdateResponse.Completed.Metadata[
                "__temporal_system_payload"]);
        Assert.DoesNotContain(
            "encoded",
            completion.Successful.Commands[0].UpdateResponse.Completed.Metadata.Keys);
        var encodedRequest = SignalWithStartWorkflowExecutionRequest.Parser.ParseFrom(
            completion.Successful.Commands[0].UpdateResponse.Completed.Data);
        Assert.Contains("encoded", encodedRequest.Input.Payloads_[0].Metadata.Keys);
    }

    [Fact]
    public async Task EncodeAsync_UnmarkedSystemNexusEnvelopeInGenericPayloadField_EncodesEnvelope()
    {
        var request = new SignalWithStartWorkflowExecutionRequest
        {
            Input = new() { Payloads_ = { new Payload { Data = ByteString.CopyFromUtf8("input") } } },
        };
        var envelope = CreateSystemEnvelope(request);
        envelope.Metadata.Remove("__temporal_system_payload");
        var completion = new WorkflowActivationCompletion
        {
            Successful = new()
            {
                Commands =
                {
                    new WorkflowCommand { UpdateResponse = new() { Completed = envelope } },
                },
            },
        };

        await WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(new MarkerPayloadCodec()), completion);

        Assert.Contains(
            "encoded",
            completion.Successful.Commands[0].UpdateResponse.Completed.Metadata.Keys);
        var encodedRequest = SignalWithStartWorkflowExecutionRequest.Parser.ParseFrom(
            completion.Successful.Commands[0].UpdateResponse.Completed.Data);
        Assert.DoesNotContain("encoded", encodedRequest.Input.Payloads_[0].Metadata.Keys);
    }

    [Fact]
    public async Task EncodeAsync_UnrecognizedMarkedSystemNexusEnvelope_FailsExplicitly()
    {
        var completion = new WorkflowActivationCompletion
        {
            Successful = new()
            {
                Commands =
                {
                    new WorkflowCommand
                    {
                        UpdateResponse = new()
                        {
                            Completed = CreateSystemEnvelope(new Google.Protobuf.WellKnownTypes.Empty()),
                        },
                    },
                },
            },
        };

        var err = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(new MarkerPayloadCodec()), completion));

        Assert.Contains("Unrecognized marked System Nexus envelope message type", err.Message);
        Assert.Contains("google.protobuf.Empty", err.Message);
    }

    [Fact]
    public async Task DecodeAsync_SystemNexusEnvelopeInGenericPayloadField_DecodesNestedPayload()
    {
        var nestedPayload = new Payload { Data = ByteString.CopyFromUtf8("input") };
        nestedPayload.Metadata["encoded"] = ByteString.Empty;
        var request = new SignalWithStartWorkflowExecutionRequest
        {
            Input = new() { Payloads_ = { nestedPayload } },
        };
        var envelope = CreateSystemEnvelope(request);
        var activation = new WorkflowActivation
        {
            Jobs =
            {
                new WorkflowActivationJob { DoUpdate = new() { Input = { envelope } } },
            },
        };

        await WorkflowCodecHelper.DecodeAsync(CreateSimpleCodecContext(new MarkerPayloadCodec()), activation);

        Assert.DoesNotContain("decoded", activation.Jobs[0].DoUpdate.Input[0].Metadata.Keys);
        var decodedRequest = SignalWithStartWorkflowExecutionRequest.Parser.ParseFrom(
            activation.Jobs[0].DoUpdate.Input[0].Data);
        Assert.Contains("decoded", decodedRequest.Input.Payloads_[0].Metadata.Keys);
    }

    [Fact]
    public async Task EncodeAsync_AllSystemPayloads_EncodesNestedPayloads()
    {
        var firstEnvelope = CreateSystemEnvelope("first");
        var secondEnvelope = CreateSystemEnvelope("second");
        var completion = new WorkflowActivationCompletion
        {
            Successful = new()
            {
                Commands =
                {
                    new WorkflowCommand
                    {
                        ScheduleActivity = new()
                        {
                            Arguments = { firstEnvelope, secondEnvelope },
                        },
                    },
                },
            },
        };

        await WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(new PackingPayloadCodec()), completion);

        var arguments = completion.Successful.Commands[0].ScheduleActivity.Arguments;
        Assert.Equal(2, arguments.Count);
        Assert.All(arguments, payload =>
        {
            Assert.Equal(ByteString.CopyFromUtf8("true"), payload.Metadata["__temporal_system_payload"]);
            var request = SignalWithStartWorkflowExecutionRequest.Parser.ParseFrom(payload.Data);
            Assert.Contains("packed", request.Input.Payloads_[0].Metadata.Keys);
        });
    }

    [Fact]
    public async Task EncodeAndDecodeAsync_MixedPayloads_PreservesSystemPayloadBoundaries()
    {
        var envelope = CreateSystemEnvelope("system");
        var completion = new WorkflowActivationCompletion
        {
            Successful = new()
            {
                Commands =
                {
                    new WorkflowCommand
                    {
                        ScheduleActivity = new()
                        {
                            Arguments =
                            {
                                new Payload { Data = ByteString.CopyFromUtf8("first") },
                                new Payload { Data = ByteString.CopyFromUtf8("second") },
                                envelope,
                                new Payload { Data = ByteString.CopyFromUtf8("third") },
                                new Payload { Data = ByteString.CopyFromUtf8("fourth") },
                            },
                        },
                    },
                },
            },
        };

        var codec = new PackingPayloadCodec();
        await WorkflowCodecHelper.EncodeAsync(CreateSimpleCodecContext(codec), completion);

        var encodedArguments = completion.Successful.Commands[0].ScheduleActivity.Arguments;
        Assert.Equal(3, encodedArguments.Count);
        Assert.Equal("first|second", encodedArguments[0].Data.ToStringUtf8());
        Assert.Equal(ByteString.CopyFromUtf8("true"), encodedArguments[1].Metadata["__temporal_system_payload"]);
        Assert.Equal("third|fourth", encodedArguments[2].Data.ToStringUtf8());

        var activation = new WorkflowActivation
        {
            Jobs =
            {
                new WorkflowActivationJob
                {
                    DoUpdate = new() { Input = { encodedArguments } },
                },
            },
        };
        await WorkflowCodecHelper.DecodeAsync(CreateSimpleCodecContext(codec), activation);

        var decodedPayloads = activation.Jobs[0].DoUpdate.Input;
        Assert.Equal(5, decodedPayloads.Count);
        Assert.Equal("first", decodedPayloads[0].Data.ToStringUtf8());
        Assert.Equal("second", decodedPayloads[1].Data.ToStringUtf8());
        Assert.Equal(ByteString.CopyFromUtf8("true"), decodedPayloads[2].Metadata["__temporal_system_payload"]);
        Assert.Equal("third", decodedPayloads[3].Data.ToStringUtf8());
        Assert.Equal("fourth", decodedPayloads[4].Data.ToStringUtf8());
        var decodedRequest = SignalWithStartWorkflowExecutionRequest.Parser.ParseFrom(decodedPayloads[2].Data);
        Assert.Equal("system", decodedRequest.Input.Payloads_[0].Data.ToStringUtf8());
    }

    private static Payload CreateSystemEnvelope(string value) => CreateSystemEnvelope(
        new SignalWithStartWorkflowExecutionRequest
        {
            Input = new() { Payloads_ = { new Payload { Data = ByteString.CopyFromUtf8(value) } } },
        });

    private static Payload CreateSystemEnvelope(IMessage message)
    {
        Assert.True(new BinaryProtoConverter().TryToPayload(message, out var payload));
        SystemNexusPayloadVisitor.MarkSystemPayload(payload!);
        return payload!;
    }

    private static WorkflowCodecHelper.WorkflowCodecContext CreateSimpleCodecContext(IPayloadCodec codec) => new(
        CodecNoContext: codec,
        CodecWorkflowContext: codec,
        Namespace: "my-namespace",
        WorkflowId: "my-workflow-id",
        WorkflowType: "my-workflow-type",
        TaskQueue: "my-task-queue",
        Instance: null);

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

    private class MarkerNoClonePayloadCodec : IPayloadCodec
    {
        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var newP = p;
                newP.Metadata["encoded"] = ByteString.Empty;
                return newP;
            }).ToList());

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var newP = p;
                newP.Metadata["decoded"] = ByteString.Empty;
                return newP;
            }).ToList());
    }

    private class PackingPayloadCodec : IPayloadCodec
    {
        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(new[]
            {
                new Payload
                {
                    Data = ByteString.CopyFromUtf8(string.Join("|", payloads.Select(p => p.Data.ToStringUtf8()))),
                    Metadata = { ["packed"] = ByteString.CopyFromUtf8(payloads.Count.ToString()) },
                },
            });

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.SelectMany(payload =>
                payload.Data.ToStringUtf8().Split('|').Select(value =>
                    new Payload { Data = ByteString.CopyFromUtf8(value) })).ToList());
    }
}
