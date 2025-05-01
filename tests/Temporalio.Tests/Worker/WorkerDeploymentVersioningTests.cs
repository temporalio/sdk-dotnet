using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Worker;

public class WorkerDeploymentVersioningTests : WorkflowEnvironmentTestBase
{
    public WorkerDeploymentVersioningTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow("DeploymentVersioningWorkflow", VersioningBehavior = VersioningBehavior.AutoUpgrade)]
    public class DeploymentVersioningWorkflowV1AutoUpgrade
    {
        private bool finish;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => finish);
            return "version-v1";
        }

        [WorkflowSignal]
        public async Task DoFinishAsync() => finish = true;

        [WorkflowQuery]
        public string State() => "v1";
    }

    [Workflow("DeploymentVersioningWorkflow", VersioningBehavior = VersioningBehavior.Pinned)]
    public class DeploymentVersioningWorkflowV2Pinned
    {
        private bool finish;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => finish);
            var depVer = Workflow.CurrentDeploymentVersion!;
            Assert.Equal("2.0", depVer.BuildId);
            return "version-v2";
        }

        [WorkflowSignal]
        public async Task DoFinishAsync() => finish = true;

        [WorkflowQuery]
        public string State() => "v2";
    }

    [Workflow("DeploymentVersioningWorkflow", VersioningBehavior = VersioningBehavior.AutoUpgrade)]
    public class DeploymentVersioningWorkflowV3AutoUpgrade
    {
        private bool finish;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => finish);
            return "version-v3";
        }

        [WorkflowSignal]
        public async Task DoFinishAsync() => finish = true;

        [WorkflowQuery]
        public string State() => "v3";
    }

    [Fact]
    public async Task WorkerWithDeploymentOptions_FollowsVersioningBehavior()
    {
        var deploymentName = $"deployment-{Guid.NewGuid()}";
        var workerV1 = new WorkerDeploymentVersion(deploymentName, "1.0");
        var workerV2 = new WorkerDeploymentVersion(deploymentName, "2.0");
        var workerV3 = new WorkerDeploymentVersion(deploymentName, "3.0");

        var taskQueue = $"tq-{Guid.NewGuid()}";

        // Create all three workers with different deployment versions
        using var worker1 = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(workerV1, true),
            }.AddWorkflow<DeploymentVersioningWorkflowV1AutoUpgrade>());

        using var worker2 = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(workerV2, true),
            }.AddWorkflow<DeploymentVersioningWorkflowV2Pinned>());

        using var worker3 = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(workerV3, true),
            }.AddWorkflow<DeploymentVersioningWorkflowV3AutoUpgrade>());

        var testTask = ExecuteTest(worker1, worker2, worker3);
        await Task.WhenAll(
            worker1.ExecuteAsync(() => testTask),
            worker2.ExecuteAsync(() => testTask),
            worker3.ExecuteAsync(() => testTask));

        async Task ExecuteTest(TemporalWorker w1, TemporalWorker w2, TemporalWorker w3)
        {
            // Wait for deployment version to be registered and make it current
            var describe1 = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV1);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describe1.ConflictToken, workerV1);

            // Start workflow 1 which will use v1 worker on auto-upgrade
            var wf1 = await Client.StartWorkflowAsync(
                (DeploymentVersioningWorkflowV1AutoUpgrade wf) => wf.RunAsync(),
                new(id: "basic-versioning-v1", taskQueue: taskQueue));
            Assert.Equal("v1", await wf1.QueryAsync(wf => wf.State()));

            // Set v2 as current and start workflow 2
            var describe2 = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV2);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describe2.ConflictToken, workerV2);

            var wf2 = await Client.StartWorkflowAsync(
                (DeploymentVersioningWorkflowV2Pinned wf) => wf.RunAsync(),
                new(id: "basic-versioning-v2", taskQueue: taskQueue));
            Assert.Equal("v2", await wf2.QueryAsync(wf => wf.State()));

            // Set v3 as current and start workflow 3
            var describe3 = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV3);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describe3.ConflictToken, workerV3);

            var wf3 = await Client.StartWorkflowAsync(
                (DeploymentVersioningWorkflowV3AutoUpgrade wf) => wf.RunAsync(),
                new(id: "basic-versioning-v3", taskQueue: taskQueue));
            Assert.Equal("v3", await wf3.QueryAsync(wf => wf.State()));

            // Signal all workflows to finish
            await wf1.SignalAsync(wf => wf.DoFinishAsync());
            await wf2.SignalAsync(wf => wf.DoFinishAsync());
            await wf3.SignalAsync(wf => wf.DoFinishAsync());

            // Check results
            var res1 = await wf1.GetResultAsync();
            var res2 = await wf2.GetResultAsync();
            var res3 = await wf3.GetResultAsync();

            Assert.Equal("version-v3", res1);
            Assert.Equal("version-v2", res2);
            Assert.Equal("version-v3", res3);
        }
    }

    [Fact]
    public async Task WorkerDeploymentRamp_ChangesTaskDistribution()
    {
        var deploymentName = $"deployment-ramping-{Guid.NewGuid()}";
        var v1 = new WorkerDeploymentVersion(deploymentName, "1.0");
        var v2 = new WorkerDeploymentVersion(deploymentName, "2.0");

        var taskQueue = $"tq-{Guid.NewGuid()}";

        // Create two workers with different deployment versions
        using var worker1 = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(v1, true),
            }.AddWorkflow<DeploymentVersioningWorkflowV1AutoUpgrade>());

        using var worker2 = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(v2, true),
            }.AddWorkflow<DeploymentVersioningWorkflowV2Pinned>());

        var testTask = ExecuteRampTest(worker1, worker2);
        await Task.WhenAll(
            worker1.ExecuteAsync(() => testTask),
            worker2.ExecuteAsync(() => testTask));

        async Task ExecuteRampTest(TemporalWorker w1, TemporalWorker w2)
        {
            await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, v1);
            var describeResp = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, v2);

            // Set current version to v1 and ramp v2 to 100%
            var conflictToken = (await TestUtils.SetCurrentDeploymentVersionAsync(
                Client, describeResp.ConflictToken, v1)).ConflictToken;
            conflictToken = (await TestUtils.SetRampingVersionAsync(
                Client, conflictToken, v2, 100)).ConflictToken;

            // Run workflows and verify they run on v2
            for (int i = 0; i < 3; i++)
            {
                var wf = await Client.StartWorkflowAsync(
                    (DeploymentVersioningWorkflowV2Pinned wf) => wf.RunAsync(),
                    new(id: $"versioning-ramp-100-{i}-{Guid.NewGuid()}", taskQueue: taskQueue));
                await wf.SignalAsync(wf => wf.DoFinishAsync());
                var res = await wf.GetResultAsync();
                Assert.Equal("version-v2", res);
            }

            // Set ramp to 0, expecting workflows to run on v1
            conflictToken = (await TestUtils.SetRampingVersionAsync(Client, conflictToken, v2, 0)).ConflictToken;
            for (int i = 0; i < 3; i++)
            {
                var wfa = await Client.StartWorkflowAsync(
                    (DeploymentVersioningWorkflowV1AutoUpgrade wf) => wf.RunAsync(),
                    new(id: $"versioning-ramp-0-{i}-{Guid.NewGuid()}", taskQueue: taskQueue));
                await wfa.SignalAsync(wf => wf.DoFinishAsync());
                var res = await wfa.GetResultAsync();
                Assert.Equal("version-v1", res);
            }

            // Set ramp to 50 and eventually verify workflows run on both versions
            await TestUtils.SetRampingVersionAsync(Client, conflictToken, v2, 50);

            var seenResults = new HashSet<string>();

            async Task<string> RunAndRecord()
            {
                var wf = await Client.StartWorkflowAsync(
                    (DeploymentVersioningWorkflowV1AutoUpgrade wf) => wf.RunAsync(),
                    new(id: $"versioning-ramp-50-{Guid.NewGuid()}", taskQueue: taskQueue));
                await wf.SignalAsync(wf => wf.DoFinishAsync());
                return await wf.GetResultAsync();
            }

            await AssertMore.EventuallyAsync(async () =>
            {
                var res = await RunAndRecord();
                seenResults.Add(res);
                Assert.Contains("version-v1", seenResults);
                Assert.Contains("version-v2", seenResults);
            });
        }
    }

    [Workflow(Dynamic = true, VersioningBehavior = VersioningBehavior.Pinned)]
    public class DynamicWorkflowVersioningOnDefn
    {
        [WorkflowRun]
        public async Task<string> RunAsync(IRawValue[] args) => "dynamic";
    }

    [Workflow(Dynamic = true, VersioningBehavior = VersioningBehavior.Pinned)]
    public class DynamicWorkflowVersioningOnMethod
    {
        [WorkflowRun]
        public async Task<string> RunAsync(IRawValue[] args) => "dynamic";

        [WorkflowDynamicOptions]
        public WorkflowDefinitionOptions DynamicOptions()
        {
            // Verify various workflow properties are accessible
            _ = Workflow.Instance;
            _ = Workflow.Info;
            return new()
            {
                VersioningBehavior = VersioningBehavior.AutoUpgrade,
            };
        }
    }

    [Fact]
    public async Task WorkerDeployment_DynamicWorkflow_OnDefinition()
    {
        await TestWorkerDeploymentDynamicWorkflow(
            typeof(DynamicWorkflowVersioningOnDefn),
            VersioningBehavior.Pinned);
    }

    [Fact]
    public async Task WorkerDeployment_DynamicWorkflow_OnMethod()
    {
        await TestWorkerDeploymentDynamicWorkflow(
            typeof(DynamicWorkflowVersioningOnMethod),
            VersioningBehavior.AutoUpgrade);
    }

    private async Task TestWorkerDeploymentDynamicWorkflow(
        Type workflowType,
        VersioningBehavior expectedVersioningBehavior)
    {
        var deploymentName = $"deployment-dynamic-{Guid.NewGuid()}";
        var workerV1 = new WorkerDeploymentVersion(deploymentName, "1.0");
        var taskQueue = $"tq-{Guid.NewGuid()}";

        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(workerV1, true),
            }.AddWorkflow(workflowType));

        await worker.ExecuteAsync(async () =>
        {
            var describeResp = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV1);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describeResp.ConflictToken, workerV1);

            var handle = await Client.StartWorkflowAsync(
                "cooldynamicworkflow",
                Array.Empty<object?>(),
                new(id: $"dynamic-workflow-versioning-{Guid.NewGuid()}", taskQueue: taskQueue));

            var result = await handle.GetResultAsync<string>();
            Assert.Equal("dynamic", result);

            var history = await handle.FetchHistoryAsync();

            // Check that at least one workflow task completed event has the expected versioning behavior
            Assert.Contains(history.Events, evt =>
                evt.WorkflowTaskCompletedEventAttributes != null &&
                evt.WorkflowTaskCompletedEventAttributes.VersioningBehavior ==
                  (Temporalio.Api.Enums.V1.VersioningBehavior)expectedVersioningBehavior);
        });
    }

    [Workflow]
    public class NoVersioningAnnotationWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync() => "whee";
    }

    [Workflow(Dynamic = true)]
    public class NoVersioningAnnotationDynamicWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(IRawValue[] args) => "whee";
    }

    [Fact]
    public void WorkflowsMustHaveVersioningBehavior_WhenFeatureTurnedOn()
    {
        var deploymentName = $"deployment-{Guid.NewGuid()}";
        var version = new WorkerDeploymentVersion(deploymentName, "1.0");

        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            var unused = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                {
                    DeploymentOptions = new(version, true),
                }.AddWorkflow<NoVersioningAnnotationWorkflow>());
        });
        Assert.Contains("must specify a versioning behavior", ex1.Message);

        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            var unused = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                {
                    DeploymentOptions = new(version, true),
                }.AddWorkflow<NoVersioningAnnotationDynamicWorkflow>());
        });
        Assert.Contains("must specify a versioning behavior", ex2.Message);
    }

    [Fact]
    public async Task WorkflowsCanUseDefaultVersioningBehavior()
    {
        var deploymentName = $"deployment-default-versioning-{Guid.NewGuid()}";
        var workerV1 = new WorkerDeploymentVersion(deploymentName, "1.0");
        var taskQueue = $"tq-{Guid.NewGuid()}";

        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue)
            {
                DeploymentOptions = new(workerV1, true)
                { DefaultVersioningBehavior = VersioningBehavior.Pinned },
            }.AddWorkflow<NoVersioningAnnotationWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var describeResp = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV1);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describeResp.ConflictToken, workerV1);

            var handle = await Client.StartWorkflowAsync(
                (NoVersioningAnnotationWorkflow wf) => wf.RunAsync(),
                new(id: $"default-versioning-behavior-{Guid.NewGuid()}", taskQueue: taskQueue));

            await handle.GetResultAsync();

            var history = await handle.FetchHistoryAsync();
            Assert.Contains(history.Events, evt =>
                evt.WorkflowTaskCompletedEventAttributes != null &&
                evt.WorkflowTaskCompletedEventAttributes.VersioningBehavior ==
                  Temporalio.Api.Enums.V1.VersioningBehavior.Pinned);
        });
    }

    [Fact]
    public void CannotUseOldAndNewVersioningOptionsTogether()
    {
#pragma warning disable 0618
        var deploymentName = $"deployment-{Guid.NewGuid()}";

        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            var unused = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                {
                    DeploymentOptions = new(new WorkerDeploymentVersion(deploymentName, "1.0"), true),
                    BuildId = "ooga booga",
                }.AddWorkflow<NoVersioningAnnotationWorkflow>());
        });
        Assert.Contains("BuildId cannot be used together", ex1.Message);

        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            var unused = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                {
                    DeploymentOptions = new(new WorkerDeploymentVersion(deploymentName, "1.0"), true),
                    UseWorkerVersioning = true,
                }.AddWorkflow<NoVersioningAnnotationWorkflow>());
        });
        Assert.Contains("UseWorkerVersioning cannot be used together", ex2.Message);
#pragma warning restore 0618
    }
}
