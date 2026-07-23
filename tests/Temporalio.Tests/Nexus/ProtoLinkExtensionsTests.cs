namespace Temporalio.Tests.Nexus;

using NexusRpc;
using Temporalio.Nexus;
using Xunit;

public class ProtoLinkExtensionsTests
{
    [Fact]
    public void NexusOperation_ToNexusLink_RoundTrips()
    {
        var nexusOp = new Api.Common.V1.Link.Types.NexusOperation
        {
            Namespace = "my-namespace",
            OperationId = "my-op-id",
            RunId = "my-run-id",
        };

        var nexusLink = nexusOp.ToNexusLink();
        Assert.Equal(
            Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName,
            nexusLink.Type);

        var roundTripped = nexusLink.ToNexusOperation();
        Assert.Equal(nexusOp.Namespace, roundTripped.Namespace);
        Assert.Equal(nexusOp.OperationId, roundTripped.OperationId);
        Assert.Equal(nexusOp.RunId, roundTripped.RunId);
    }

    [Fact]
    public void NexusOperation_ToNexusLink_SpecialCharactersRoundTrip()
    {
        var nexusOp = new Api.Common.V1.Link.Types.NexusOperation
        {
            Namespace = "ns/with spaces",
            OperationId = "op?id=1&foo=bar",
            RunId = "run+id",
        };

        var nexusLink = nexusOp.ToNexusLink();
        var roundTripped = nexusLink.ToNexusOperation();
        Assert.Equal(nexusOp.Namespace, roundTripped.Namespace);
        Assert.Equal(nexusOp.OperationId, roundTripped.OperationId);
        Assert.Equal(nexusOp.RunId, roundTripped.RunId);
    }

    [Fact]
    public void ToNexusOperationLink_RejectsNonTemporalScheme()
    {
        var link = new NexusLink(
            new Uri("https://somehost/namespaces/ns/nexus-operations/op/run/details"),
            Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToNexusOperation());
    }

    [Fact]
    public void ToNexusOperationLink_RejectsUnexpectedHost()
    {
        var link = new NexusLink(
            new Uri("temporal://somehost/namespaces/ns/nexus-operations/op/run/details"),
            Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToNexusOperation());
    }

    [Fact]
    public void ToNexusOperationLink_RejectsInvalidPath()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf/run/history"),
            Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToNexusOperation());
    }

    [Fact]
    public void ToProtoLink_NexusOperationShape_RoundTrips()
    {
        var nexusOp = new Api.Common.V1.Link.Types.NexusOperation
        {
            Namespace = "ns",
            OperationId = "op-id",
            RunId = "run-id",
        };
        var protoLink = nexusOp.ToNexusLink().ToProtoLink();
        Assert.Equal(nexusOp, protoLink.NexusOperation);
    }

    [Fact]
    public void ToProtoLink_WorkflowEventShape_RoundTrips()
    {
        var wfEvent = new Api.Common.V1.Link.Types.WorkflowEvent
        {
            Namespace = "ns",
            WorkflowId = "wf",
            RunId = "run-id",
            EventRef = new() { EventId = 1, EventType = Api.Enums.V1.EventType.WorkflowExecutionStarted },
        };
        var protoLink = wfEvent.ToNexusLink().ToProtoLink();
        Assert.Equal(wfEvent, protoLink.WorkflowEvent);
    }

    [Fact]
    public void ToProtoLink_RejectsUnknownType()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/nexus-operations/op/run/details"),
            "some.unknown.LinkType");
        Assert.Throws<ArgumentException>(() => link.ToProtoLink());
    }

    [Fact]
    public void ProtoToNexusLink_NexusOperationVariant_Dispatches()
    {
        var protoLink = new Api.Common.V1.Link
        {
            NexusOperation = new() { Namespace = "ns", OperationId = "op", RunId = "run" },
        };
        var nexusLink = protoLink.ToNexusLink();
        Assert.NotNull(nexusLink);
        Assert.Equal(
            Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName,
            nexusLink.Type);
        Assert.Equal(protoLink.NexusOperation, nexusLink.ToNexusOperation());
    }

    [Fact]
    public void ProtoToNexusLink_WorkflowEventVariant_Dispatches()
    {
        var protoLink = new Api.Common.V1.Link
        {
            WorkflowEvent = new()
            {
                Namespace = "ns",
                WorkflowId = "wf",
                RunId = "run",
                EventRef = new() { EventId = 1, EventType = Api.Enums.V1.EventType.WorkflowExecutionStarted },
            },
        };
        var nexusLink = protoLink.ToNexusLink();
        Assert.NotNull(nexusLink);
        Assert.Equal(
            Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName,
            nexusLink.Type);
        Assert.Equal(protoLink.WorkflowEvent, nexusLink.ToWorkflowEvent());
    }

    [Fact]
    public void ProtoToNexusLink_UnsetVariant_ReturnsNull()
    {
        // An unset link variant (e.g. a rejected update with no history event) converts to null so
        // callers can skip it rather than failing the operation.
        Assert.Null(new Api.Common.V1.Link().ToNexusLink());
    }

    [Fact]
    public void WorkflowEvent_ToNexusLink_RoundTrips()
    {
        var wfEvent = new Api.Common.V1.Link.Types.WorkflowEvent
        {
            Namespace = "my-namespace",
            WorkflowId = "my-wf",
            RunId = "my-run-id",
            EventRef = new() { EventId = 1, EventType = Api.Enums.V1.EventType.WorkflowExecutionStarted },
        };

        var nexusLink = wfEvent.ToNexusLink();
        Assert.Equal(
            Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName,
            nexusLink.Type);

        var roundTripped = nexusLink.ToWorkflowEvent();
        Assert.Equal(wfEvent.Namespace, roundTripped.Namespace);
        Assert.Equal(wfEvent.WorkflowId, roundTripped.WorkflowId);
        Assert.Equal(wfEvent.RunId, roundTripped.RunId);
        Assert.Equal(wfEvent.EventRef.EventId, roundTripped.EventRef.EventId);
        Assert.Equal(wfEvent.EventRef.EventType, roundTripped.EventRef.EventType);
    }
}
