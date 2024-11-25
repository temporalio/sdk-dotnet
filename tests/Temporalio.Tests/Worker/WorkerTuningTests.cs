#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Worker.Tuning;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Worker;

public class WorkerTuningTests : WorkflowEnvironmentTestBase
{
    public WorkerTuningTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class SimpleWorkflow
    {
        [Activity]
        public static string SomeActivity() => "hi";

        [WorkflowRun]
        public async Task<string> RunAsync(string name)
        {
            var activities = Enumerable.Range(1, 10)
                .Select(_ => Workflow.ExecuteActivityAsync(
                    () => SomeActivity(),
                    new() { StartToCloseTimeout = TimeSpan.FromMinutes(1) })).ToList();
            await Task.WhenAll(activities);
            return "Hi!";
        }
    }

    [Fact]
    public async Task CanRunWith_ResourceBasedTuner()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                Tuner = WorkerTuner.CreateResourceBased(0.5, 0.5),
            }.AddWorkflow<SimpleWorkflow>().AddActivity(SimpleWorkflow.SomeActivity));
        await worker.ExecuteAsync(async () =>
        {
            await Env.Client.ExecuteWorkflowAsync(
                (SimpleWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        });
    }

    [Fact]
    public async Task CanRunWith_CompositeTuner()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                Tuner = new WorkerTuner(
                    new FixedSizeSlotSupplier(10),
                    new ResourceBasedSlotSupplier(
                        new ResourceBasedSlotSupplierOptions(),
                        new ResourceBasedTunerOptions(0.5, 0.5)),
                    new FixedSizeSlotSupplier(20)),
            }.AddWorkflow<SimpleWorkflow>().AddActivity(SimpleWorkflow.SomeActivity));
        await worker.ExecuteAsync(async () =>
        {
            await Env.Client.ExecuteWorkflowAsync(
                (SimpleWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        });
    }

    [Fact]
    public async Task Cannot_Supply_Different_TunerOptions()
    {
        var argumentException = Assert.Throws<ArgumentException>(() =>
        {
            var unused = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                {
                    Tuner = new WorkerTuner(
                        new FixedSizeSlotSupplier(10),
                        new ResourceBasedSlotSupplier(
                            new ResourceBasedSlotSupplierOptions(),
                            new ResourceBasedTunerOptions(0.5, 0.5)),
                        new ResourceBasedSlotSupplier(
                            new ResourceBasedSlotSupplierOptions(),
                            new ResourceBasedTunerOptions(0.2, 0.2))),
                }.AddWorkflow<SimpleWorkflow>());
        });
        Assert.Contains("same ResourceBasedTunerOptions", argumentException.Message);
    }

    [Fact]
    public async Task Cannot_Mix_MaxConcurrent_And_Tuner()
    {
        var argumentException = Assert.Throws<ArgumentException>(() =>
        {
            var unused = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                {
                    Tuner = WorkerTuner.CreateResourceBased(0.5, 0.5),
                    MaxConcurrentActivities = 30,
                }.AddWorkflow<SimpleWorkflow>());
        });
        Assert.Contains("Cannot set both Tuner and any of", argumentException.Message);
    }

    class MySlotSupplier : ICustomSlotSupplier
    {
        public Task<SlotPermit> ReserveSlotAsync(SlotReserveContext ctx)
        {
            throw new NotImplementedException();
        }

        public SlotPermit? TryReserveSlot(SlotReserveContext ctx)
        {
            throw new NotImplementedException();
        }

        public void MarkSlotUsed(SlotMarkUsedContext ctx)
        {
            throw new NotImplementedException();
        }

        public void ReleaseSlot(SlotReleaseContext ctx)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task CanRunWith_CustomSlotSupplier()
    {
        var mySlotSupplier = new MySlotSupplier();
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                Tuner = new WorkerTuner(mySlotSupplier, mySlotSupplier, mySlotSupplier),
            }.AddWorkflow<SimpleWorkflow>().AddActivity(SimpleWorkflow.SomeActivity));
        await worker.ExecuteAsync(async () =>
        {
            await Env.Client.ExecuteWorkflowAsync(
                (SimpleWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        });
    }
}
