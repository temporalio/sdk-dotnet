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
    public void ForwardLinks_DefaultsToEmpty()
    {
        var context = NewContext();
        Assert.Empty(context.ForwardLinks);
    }

    [Fact]
    public void ForwardLinks_RoundTrips()
    {
        var context = NewContext();
        var links = new[] { WorkflowEventLink("wf", "run", EventType.NexusOperationScheduled) };
        context.ForwardLinks = links;
        Assert.Equal(links, context.ForwardLinks);
    }

    [Fact]
    public void TryAddBacklink_AppendsWorkflowEventLink()
    {
        var context = NewContext();
        var link = WorkflowEventLink("wf", "run", EventType.WorkflowExecutionSignaled);
        Assert.True(context.TryAddBacklink(link));
        Assert.Equal(new[] { link }, context.Backlinks);
    }

    [Fact]
    public void TryAddBacklink_AccumulatesInOrder()
    {
        var context = NewContext();
        var first = WorkflowEventLink("a", "run-a", EventType.WorkflowExecutionSignaled);
        var second = WorkflowEventLink("b", "run-b", EventType.WorkflowExecutionStarted);
        context.TryAddBacklink(first);
        context.TryAddBacklink(second);
        Assert.Equal(new[] { first, second }, context.Backlinks);
    }

    [Fact]
    public void TryAddBacklink_IgnoresNull()
    {
        var context = NewContext();
        Assert.False(context.TryAddBacklink(null));
        Assert.Empty(context.Backlinks);
    }

    [Fact]
    public void TryAddBacklink_IgnoresNonWorkflowEventLink()
    {
        var context = NewContext();
        // A link with no variant set (e.g. an unset response link) must be dropped.
        Assert.False(context.TryAddBacklink(new Link()));
        // A non-WorkflowEvent variant must also be dropped.
        Assert.False(context.TryAddBacklink(new Link { BatchJob = new() { JobId = "batch" } }));
        Assert.Empty(context.Backlinks);
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
