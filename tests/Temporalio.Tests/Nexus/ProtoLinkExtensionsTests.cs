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

    [Fact]
    public void ActivityLink_ToNexusLink_BuildsExpectedUri()
    {
        var act = new Api.Common.V1.Link.Types.Activity
        {
            Namespace = "my-ns",
            ActivityId = "my-aid",
            RunId = "my-run",
        };
        var nexusLink = act.ToNexusLink();

        Assert.Equal("temporal", nexusLink.Uri.Scheme);
        Assert.Equal(Api.Common.V1.Link.Types.Activity.Descriptor.FullName, nexusLink.Type);
        Assert.Equal(
            "/namespaces/my-ns/activities/my-aid/my-run/details",
            nexusLink.Uri.AbsolutePath);
    }

    [Fact]
    public void ToActivity_ParsesServerStyleUri()
    {
        // Servers produce URIs in the host-less form `temporal:/namespaces/.../details`.
        var link = new NexusLink(
            new Uri("temporal:/namespaces/my-ns/activities/my-aid/my-run/details"),
            Api.Common.V1.Link.Types.Activity.Descriptor.FullName);

        var act = link.ToActivity();
        Assert.Equal("my-ns", act.Namespace);
        Assert.Equal("my-aid", act.ActivityId);
        Assert.Equal("my-run", act.RunId);
    }

    [Fact]
    public void ToActivity_RejectsNonTemporalScheme()
    {
        var link = new NexusLink(
            new Uri("https://example/namespaces/ns/activities/aid/run/details"),
            Api.Common.V1.Link.Types.Activity.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToActivity());
    }

    [Fact]
    public void ToActivity_RejectsBadPath()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wid/run/history"),
            Api.Common.V1.Link.Types.Activity.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToActivity());
    }

    [Fact]
    public void WorkflowLink_ToNexusLink_BuildsExpectedUri()
    {
        // A workflow link addresses the execution itself, so unlike a workflow-event link there is
        // no "/history" suffix. That absence is the only thing distinguishing the two paths.
        var workflow = new Api.Common.V1.Link.Types.Workflow
        {
            Namespace = "ns",
            WorkflowId = "wf-id",
            RunId = "run-id",
        };
        var nexusLink = workflow.ToNexusLink();

        Assert.Equal("temporal", nexusLink.Uri.Scheme);
        Assert.Equal(Api.Common.V1.Link.Types.Workflow.Descriptor.FullName, nexusLink.Type);
        Assert.Equal("/namespaces/ns/workflows/wf-id/run-id", nexusLink.Uri.AbsolutePath);
        Assert.Equal(string.Empty, nexusLink.Uri.Query);
    }

    [Fact]
    public void WorkflowLink_ToNexusLink_EncodesReasonAsQueryParam()
    {
        var workflow = new Api.Common.V1.Link.Types.Workflow
        {
            Namespace = "ns",
            WorkflowId = "wf-id",
            RunId = "run-id",
            Reason = "rejected update",
        };
        var nexusLink = workflow.ToNexusLink();

        Assert.Equal("/namespaces/ns/workflows/wf-id/run-id", nexusLink.Uri.AbsolutePath);
        Assert.Equal("?reason=rejected%20update", nexusLink.Uri.Query);
    }

    [Fact]
    public void WorkflowLink_ToNexusLink_EscapesPathSegments()
    {
        // A slash and a space in the path must be percent escaped, otherwise the link resolves to a
        // different workflow.
        var workflow = new Api.Common.V1.Link.Types.Workflow
        {
            Namespace = "ns",
            WorkflowId = "wf/id with space",
            RunId = "run-id",
        };
        var nexusLink = workflow.ToNexusLink();

        Assert.Equal(
            "/namespaces/ns/workflows/wf%2Fid%20with%20space/run-id",
            nexusLink.Uri.AbsolutePath);
    }

    [Fact]
    public void ToWorkflow_ParsesUri()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        var workflow = link.ToWorkflow();
        Assert.Equal("ns", workflow.Namespace);
        Assert.Equal("wf-id", workflow.WorkflowId);
        Assert.Equal("run-id", workflow.RunId);
        Assert.Equal(string.Empty, workflow.Reason);
    }

    [Fact]
    public void ToWorkflow_ParsesReason()
    {
        // Other SDKs form encode this param, so a "+" has to decode back to a space.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?reason=rejected+update"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal("rejected update", link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_ParsesPercentEncodedReason()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?reason=rejected%20update"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal("rejected update", link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_RejectsTrailingSegment()
    {
        // The workflow-event form addresses an event inside the workflow, so it must not be accepted
        // as a workflow link even when the type says otherwise.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id/history"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToWorkflow());
    }

    [Fact]
    public void ToWorkflow_RejectsMissingRunId()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToWorkflow());
    }

    [Fact]
    public void ToWorkflow_RejectsNonTemporalScheme()
    {
        var link = new NexusLink(
            new Uri("https://example/namespaces/ns/workflows/wf-id/run-id"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToWorkflow());
    }

    [Fact]
    public void ToWorkflow_FindsReasonByKeyNotPosition()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?foo=bar&reason=Query+processed"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal("Query processed", link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_EmptyReasonValue()
    {
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?reason="),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal(string.Empty, link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_BareReasonKey()
    {
        // A key with no "=" must not blow up on the missing value.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?reason"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal(string.Empty, link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_ReasonPrefixKeyIgnored()
    {
        // "reasonx" must not be treated as "reason".
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?reasonx=nope"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal(string.Empty, link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_LiteralPlusInPathIsPreserved()
    {
        // A "+" in a path segment is a literal "+", not a space. Path segments are percent decoded
        // only; form decoding applies to query values.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/a+b/run-id"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal("a+b", link.ToWorkflow().WorkflowId);
    }

    [Fact]
    public void WorkflowLink_RoundTrips()
    {
        // Reserved characters in every field at once: path segments are percent escaped and the
        // reason is a query value, so a reason containing "=" and "&" must not be split as syntax.
        var workflow = new Api.Common.V1.Link.Types.Workflow
        {
            Namespace = "ns/with/slash",
            WorkflowId = "wf id with space",
            RunId = "run-id",
            Reason = "reason with = and &",
        };

        var roundTripped = workflow.ToNexusLink().ToWorkflow();
        Assert.Equal(workflow, roundTripped);
    }

    [Fact]
    public void ToProtoLink_WorkflowShape_RoundTrips()
    {
        var workflow = new Api.Common.V1.Link.Types.Workflow
        {
            Namespace = "ns",
            WorkflowId = "wf",
            RunId = "run-id",
            Reason = "Query processed",
        };
        var protoLink = workflow.ToNexusLink().ToProtoLink();
        Assert.Equal(workflow, protoLink.Workflow);
    }

    [Fact]
    public void ProtoToNexusLink_WorkflowVariant_Dispatches()
    {
        var protoLink = new Api.Common.V1.Link
        {
            Workflow = new() { Namespace = "ns", WorkflowId = "wf", RunId = "run" },
        };
        var nexusLink = protoLink.ToNexusLink();
        Assert.NotNull(nexusLink);
        Assert.Equal(
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName,
            nexusLink.Type);
        Assert.Equal(protoLink.Workflow, nexusLink.ToWorkflow());
    }

    [Fact]
    public void ToWorkflowEvent_RejectsSuffixlessWorkflowPath()
    {
        // The inverse of ToWorkflow_RejectsTrailingSegment: a workflow link must not be readable as
        // a workflow event.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id"),
            Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName);
        Assert.Throws<ArgumentException>(() => link.ToWorkflowEvent());
    }

    [Fact]
    public void WorkflowLink_LiteralPlusInReason_RoundTrips()
    {
        // Form encoding writes a space as "+" and a literal "+" as "%2B", so the reader has to
        // replace "+" with a space before percent decoding. Doing it in the other order would turn
        // this reason into "a b".
        var workflow = new Api.Common.V1.Link.Types.Workflow
        {
            Namespace = "ns",
            WorkflowId = "wf-id",
            RunId = "run-id",
            Reason = "a+b",
        };
        var nexusLink = workflow.ToNexusLink();

        Assert.Equal("?reason=a%2Bb", nexusLink.Uri.Query);
        Assert.Equal("a+b", nexusLink.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflow_FormEncodedLiteralPlusInReason()
    {
        // The same case as written by an SDK that form encodes the whole query string.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id?reason=a%2Bb"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal("a+b", link.ToWorkflow().Reason);
    }

    [Fact]
    public void ToWorkflowEvent_FormDecodesQueryValues()
    {
        // Query values are form decoded. Request IDs are UUIDs in practice,
        // so this is about cross-SDK consistency rather than a case that arises today.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/run-id/history" +
                "?referenceType=RequestIdReference&requestID=a+b" +
                "&eventType=WorkflowExecutionStarted"),
            Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName);

        Assert.Equal("a b", link.ToWorkflowEvent().RequestIdRef.RequestId);
    }

    [Fact]
    public void ToWorkflow_AcceptsEmptyRunId()
    {
        // Characterization, not a statement of intent. A trailing slash still yields the expected
        // segment count, so the run ID comes back empty rather than being rejected. The same
        // leniency exists in the workflow-event, activity, and nexus-operation converters, since
        // they share this path parsing, so tightening it is a decision about all four rather than
        // about this converter.
        var link = new NexusLink(
            new Uri("temporal:///namespaces/ns/workflows/wf-id/"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        Assert.Equal(string.Empty, link.ToWorkflow().RunId);
    }

    [Fact]
    public void ToWorkflow_AcceptsEmptyNamespace()
    {
        // Characterization; see ToWorkflow_AcceptsEmptyRunId.
        var link = new NexusLink(
            new Uri("temporal:///namespaces//workflows/wf-id/run-id"),
            Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);

        var workflow = link.ToWorkflow();
        Assert.Equal(string.Empty, workflow.Namespace);
        Assert.Equal("wf-id", workflow.WorkflowId);
        Assert.Equal("run-id", workflow.RunId);
    }
}
