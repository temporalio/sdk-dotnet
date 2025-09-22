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

    [Workflow]
    public class OneTaskWf
    {
        [WorkflowRun]
        public async Task<string> RunAsync()
        {
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
                    new FixedSizeSlotSupplier(20),
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
                            new ResourceBasedTunerOptions(0.2, 0.2)),
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

    private class MySlotSupplier : CustomSlotSupplier
    {
        private object lockObj = new();

        public uint ReserveCount { get; private set; }

        public uint ReleaseCount { get; private set; }

        public uint BiggestReleasedPermit { get; private set; }

        public bool SawWFSlotInfo { get; private set; }

        public bool SawActSlotInfo { get; private set; }

        public HashSet<SlotType> SeenReserveTypes { get; } = new();

        public HashSet<string> SeenActivityTypes { get; } = new();

        public HashSet<string> SeenWorkflowTypes { get; } = new();

        public HashSet<bool> SeenStickyTypes { get; } = new();

        public HashSet<bool> SeenReleaseInfoPresence { get; } = new();

        public override async Task<SlotPermit> ReserveSlotAsync(SlotReserveContext ctx, CancellationToken cancellationToken)
        {
            // Do something async to make sure that works
            await Task.Delay(10, cancellationToken);
            ReserveTracking(ctx);
            return new SlotPermit(ReserveCount);
        }

        public override SlotPermit? TryReserveSlot(SlotReserveContext ctx)
        {
            ReserveTracking(ctx);
            return new SlotPermit(ReserveCount);
        }

        public override void MarkSlotUsed(SlotMarkUsedContext ctx)
        {
            lock (lockObj)
            {
                switch (ctx.SlotInfo)
                {
                    case Temporalio.Worker.Tuning.SlotInfo.WorkflowSlotInfo wsi:
                        SawWFSlotInfo = true;
                        SeenWorkflowTypes.Add(wsi.WorkflowType);
                        break;
                    case Temporalio.Worker.Tuning.SlotInfo.ActivitySlotInfo asi:
                        SawActSlotInfo = true;
                        SeenActivityTypes.Add(asi.ActivityType);
                        break;
                    case Temporalio.Worker.Tuning.SlotInfo.LocalActivitySlotInfo lasi:
                        break;
                }
            }
        }

        public override void ReleaseSlot(SlotReleaseContext ctx)
        {
            var dat = (uint)ctx.Permit.UserData!;
            lock (lockObj)
            {
                ReleaseCount++;
                SeenReleaseInfoPresence.Add(ctx.SlotInfo == null);
                if (dat > BiggestReleasedPermit)
                {
                    BiggestReleasedPermit = dat;
                }
            }
        }

        private void ReserveTracking(SlotReserveContext ctx)
        {
            lock (lockObj)
            {
                ReserveCount++;
                SeenStickyTypes.Add(ctx.IsSticky);
                SeenReserveTypes.Add(ctx.SlotType);
            }
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
                Tuner = new WorkerTuner(mySlotSupplier, mySlotSupplier, mySlotSupplier, mySlotSupplier),
            }.AddWorkflow<SimpleWorkflow>().AddActivity(SimpleWorkflow.SomeActivity));
        await worker.ExecuteAsync(async () =>
        {
            await Env.Client.ExecuteWorkflowAsync(
                (SimpleWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        });
        Assert.Equal(mySlotSupplier.ReleaseCount, mySlotSupplier.BiggestReleasedPermit);
        Assert.True(mySlotSupplier.SawWFSlotInfo);
        Assert.True(mySlotSupplier.SawActSlotInfo);
        Assert.Contains("SimpleWorkflow", mySlotSupplier.SeenWorkflowTypes);
        Assert.Contains("SomeActivity", mySlotSupplier.SeenActivityTypes);
        Assert.Equal(3, mySlotSupplier.SeenReserveTypes.Count);
        Assert.Equal(2, mySlotSupplier.SeenReleaseInfoPresence.Count);
    }

    private class ThrowingSlotSupplier : CustomSlotSupplier
    {
        public override Task<SlotPermit> ReserveSlotAsync(SlotReserveContext ctx, CancellationToken cancellationToken)
        {
            // Let the workflow complete, but other reservations fail
            if (ctx.SlotType == SlotType.Workflow)
            {
                return Task.FromResult<SlotPermit>(new SlotPermit(1));
            }
            throw new InvalidOperationException("ReserveSlot");
        }

        public override SlotPermit? TryReserveSlot(SlotReserveContext ctx)
        {
            throw new InvalidOperationException("TryReserveSlot");
        }

        public override void MarkSlotUsed(SlotMarkUsedContext ctx)
        {
            throw new InvalidOperationException("MarkSlotUsed");
        }

        public override void ReleaseSlot(SlotReleaseContext ctx)
        {
            throw new InvalidOperationException("ReleaseSlot");
        }
    }

    [Fact]
    public async Task CanRunWith_ThrowingSlotSupplier()
    {
        var mySlotSupplier = new ThrowingSlotSupplier();
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                Tuner = new WorkerTuner(mySlotSupplier, mySlotSupplier, mySlotSupplier, mySlotSupplier),
            }.AddWorkflow<OneTaskWf>());
        await worker.ExecuteAsync(async () =>
        {
            await Env.Client.ExecuteWorkflowAsync(
                (OneTaskWf wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        });
    }

    private class BlockingSlotSupplier : CustomSlotSupplier
    {
        public override async Task<SlotPermit> ReserveSlotAsync(SlotReserveContext ctx, CancellationToken cancellationToken)
        {
            await Task.Delay(100_000, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Should not be reachable");
        }

        public override SlotPermit? TryReserveSlot(SlotReserveContext ctx)
        {
            return null;
        }

        public override void MarkSlotUsed(SlotMarkUsedContext ctx)
        {
        }

        public override void ReleaseSlot(SlotReleaseContext ctx)
        {
        }
    }

    [Fact]
    public async Task CanRunWith_BlockingSlotSupplier()
    {
        var mySlotSupplier = new BlockingSlotSupplier();
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                Tuner = new WorkerTuner(mySlotSupplier, mySlotSupplier, mySlotSupplier, mySlotSupplier),
            }.AddWorkflow<OneTaskWf>());
        await worker.ExecuteAsync(async () =>
        {
            await Task.Delay(1000);
        });
    }
}
