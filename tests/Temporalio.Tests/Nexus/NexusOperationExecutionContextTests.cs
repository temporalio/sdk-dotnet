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
    public void RequestLinks_DefaultsToEmpty()
    {
        var context = NewContext();
        Assert.Empty(context.RequestLinks);
    }

    [Fact]
    public void RequestLinks_RoundTrips()
    {
        var context = NewContext();
        var links = new[] { WorkflowEventLink("wf", "run", EventType.NexusOperationScheduled) };
        context.RequestLinks = links;
        Assert.Equal(links, context.RequestLinks);
    }

    [Fact]
    public void TryAddResponseLink_AppendsWorkflowEventLink()
    {
        var context = NewContext();
        var link = WorkflowEventLink("wf", "run", EventType.WorkflowExecutionSignaled);
        Assert.True(context.TryAddResponseLink(link));
        Assert.Equal(new[] { link }, context.ResponseLinks);
    }

    [Fact]
    public void TryAddResponseLink_AccumulatesInOrder()
    {
        var context = NewContext();
        var first = WorkflowEventLink("a", "run-a", EventType.WorkflowExecutionSignaled);
        var second = WorkflowEventLink("b", "run-b", EventType.WorkflowExecutionStarted);
        context.TryAddResponseLink(first);
        context.TryAddResponseLink(second);
        Assert.Equal(new[] { first, second }, context.ResponseLinks);
    }

    [Fact]
    public void TryAddResponseLink_IgnoresNull()
    {
        var context = NewContext();
        Assert.False(context.TryAddResponseLink(null));
        Assert.Empty(context.ResponseLinks);
    }

    [Fact]
    public void TryAddResponseLink_IgnoresNonWorkflowEventLink()
    {
        var context = NewContext();
        // A link with no variant set (e.g. an unset response link) must be dropped.
        Assert.False(context.TryAddResponseLink(new Link()));
        // A non-WorkflowEvent variant must also be dropped.
        Assert.False(context.TryAddResponseLink(new Link { BatchJob = new() { JobId = "batch" } }));
        Assert.Empty(context.ResponseLinks);
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
