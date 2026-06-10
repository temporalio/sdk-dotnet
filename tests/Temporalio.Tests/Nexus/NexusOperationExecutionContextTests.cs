namespace Temporalio.Tests.Nexus;

using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;
using Temporalio.Nexus;
using Xunit;

public class NexusOperationExecutionContextTests
{
    [Fact]
    public void NexusOperationLinks_DefaultsToEmpty()
    {
        var context = NewContext();
        Assert.Empty(context.NexusOperationLinks);
    }

    [Fact]
    public void NexusOperationLinks_RoundTrips()
    {
        var context = NewContext();
        var links = new[] { WorkflowEventLink("wf", "run", EventType.NexusOperationScheduled) };
        context.NexusOperationLinks = links;
        Assert.Equal(links, context.NexusOperationLinks);
    }

    [Fact]
    public void NexusOperationLinks_NullResetsToEmpty()
    {
        var context = NewContext();
        context.NexusOperationLinks =
            new[] { WorkflowEventLink("wf", "run", EventType.NexusOperationScheduled) };
        context.NexusOperationLinks = null!;
        Assert.Empty(context.NexusOperationLinks);
    }

    [Fact]
    public void AddBacklink_AppendsWorkflowEventLink()
    {
        var context = NewContext();
        var link = WorkflowEventLink("wf", "run", EventType.WorkflowExecutionSignaled);
        context.AddBacklink(link);
        Assert.Equal(new[] { link }, context.ResponseBacklinks);
    }

    [Fact]
    public void AddBacklink_AccumulatesInOrder()
    {
        var context = NewContext();
        var first = WorkflowEventLink("a", "run-a", EventType.WorkflowExecutionSignaled);
        var second = WorkflowEventLink("b", "run-b", EventType.WorkflowExecutionStarted);
        context.AddBacklink(first);
        context.AddBacklink(second);
        Assert.Equal(new[] { first, second }, context.ResponseBacklinks);
    }

    [Fact]
    public void AddBacklink_IgnoresNull()
    {
        var context = NewContext();
        context.AddBacklink(null);
        Assert.Empty(context.ResponseBacklinks);
    }

    [Fact]
    public void AddBacklink_IgnoresNonWorkflowEventLink()
    {
        var context = NewContext();
        // A link with no variant set (e.g. an unset response link) must be dropped.
        context.AddBacklink(new Link());
        // A non-WorkflowEvent variant must also be dropped.
        context.AddBacklink(new Link { BatchJob = new() { JobId = "batch" } });
        Assert.Empty(context.ResponseBacklinks);
    }

    private static NexusOperationExecutionContext NewContext()
    {
        var handlerContext = new OperationStartContext(
            Service: "svc",
            Operation: "op",
            CancellationToken: CancellationToken.None,
            RequestId: Guid.NewGuid().ToString());
        return new NexusOperationExecutionContext(
            handlerContext: handlerContext,
            info: new("ns", "tq", "endpoint"),
            logger: NullLogger.Instance,
            runtimeMetricMeter: new Lazy<MetricMeter>(
                () => throw new InvalidOperationException("metric meter not expected in test")),
            temporalClient: null);
    }

    private static Link WorkflowEventLink(string workflowId, string runId, EventType eventType) =>
        new()
        {
            WorkflowEvent = new()
            {
                Namespace = "ns",
                WorkflowId = workflowId,
                RunId = runId,
                EventRef = new() { EventType = eventType },
            },
        };
}
