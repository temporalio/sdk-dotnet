namespace Temporalio.Tests.Extensions.Aws.Lambda;

using Amazon.Lambda.Core;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Extensions.Aws.Lambda;
using Temporalio.Worker;
using Temporalio.Worker.Tuning;
using Xunit;

public class TemporalLambdaWorkerTests
{
    private static readonly WorkerDeploymentVersion Version = new("deployment", "build");

    [Fact]
    public async Task CreateHandler_DefaultsAreAppliedAndUserOverridesWin()
    {
        var configureCalls = 0;
        TemporalClientConnectOptions? capturedClientOptions = null;
        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            config =>
            {
                configureCalls++;
                Assert.Equal(2, config.WorkerOptions.MaxConcurrentActivities);
                Assert.Equal(10, config.WorkerOptions.MaxConcurrentWorkflowTasks);
                Assert.Equal(2, config.WorkerOptions.MaxConcurrentLocalActivities);
                Assert.Equal(5, config.WorkerOptions.MaxConcurrentNexusTasks);
                Assert.Equal(TimeSpan.FromSeconds(5), config.WorkerOptions.GracefulShutdownTimeout);
                Assert.Equal(30, config.WorkerOptions.MaxCachedWorkflows);
                Assert.Equal(2, SimpleMaximum(config.WorkerOptions.WorkflowTaskPollerBehavior));
                Assert.Equal(1, SimpleMaximum(config.WorkerOptions.ActivityTaskPollerBehavior));
                Assert.Equal(1, SimpleMaximum(config.WorkerOptions.NexusTaskPollerBehavior));
                Assert.True(config.WorkerOptions.DisableEagerActivityExecution);
                Assert.Equal("env-task-queue", config.WorkerOptions.TaskQueue);

                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "configured-task-queue";
                config.WorkerOptions.MaxConcurrentActivities = 8;
                config.WorkerOptions.MaxCachedWorkflows = 12;
                config.WorkerOptions.WorkflowTaskPollerBehavior =
                    new PollerBehavior.SimpleMaximum(4);
                config.WorkerOptions.DisableEagerActivityExecution = false;
                config.WorkerOptions.DeploymentOptions = new WorkerDeploymentOptions(
                    new WorkerDeploymentVersion("ignored", "ignored"),
                    useWorkerVersioning: false)
                {
                    DefaultVersioningBehavior = VersioningBehavior.AutoUpgrade,
                };
                config.WorkerOptions.Activities.Add(DummyActivity());
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                GetEnvironmentVariable = name =>
                    name == "TEMPORAL_TASK_QUEUE" ? "env-task-queue" : null,
                ConnectClientAsync = options =>
                {
                    capturedClientOptions = options;
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = options;
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        Assert.Equal(1, configureCalls);
        await handler(null, new FakeLambdaContext());

        Assert.NotNull(capturedClientOptions);
        Assert.NotNull(capturedWorkerOptions);
        Assert.Equal("configured-task-queue", capturedWorkerOptions.TaskQueue);
        Assert.Equal(8, capturedWorkerOptions.MaxConcurrentActivities);
        Assert.Equal(10, capturedWorkerOptions.MaxConcurrentWorkflowTasks);
        Assert.Equal(2, capturedWorkerOptions.MaxConcurrentLocalActivities);
        Assert.Equal(5, capturedWorkerOptions.MaxConcurrentNexusTasks);
        Assert.Equal(12, capturedWorkerOptions.MaxCachedWorkflows);
        Assert.Equal(4, SimpleMaximum(capturedWorkerOptions.WorkflowTaskPollerBehavior));
        Assert.False(capturedWorkerOptions.DisableEagerActivityExecution);
        Assert.NotNull(capturedWorkerOptions.DeploymentOptions);
        Assert.Equal(Version, capturedWorkerOptions.DeploymentOptions.Version);
        Assert.True(capturedWorkerOptions.DeploymentOptions.UseWorkerVersioning);
        Assert.Equal(
            VersioningBehavior.AutoUpgrade,
            capturedWorkerOptions.DeploymentOptions.DefaultVersioningBehavior);
#pragma warning disable CS0618 // Verifying the Lambda helper clears legacy versioning options.
        Assert.Null(capturedWorkerOptions.BuildId);
        Assert.False(capturedWorkerOptions.UseWorkerVersioning);
#pragma warning restore CS0618
    }

    [Fact]
    public void CreateHandler_MissingDeploymentNameOrBuildIdThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            TemporalLambdaWorker.CreateHandler(
                new WorkerDeploymentVersion(string.Empty, "build"),
                _ => { }));
        Assert.Throws<ArgumentException>(() =>
            TemporalLambdaWorker.CreateHandler(
                new WorkerDeploymentVersion("deployment", string.Empty),
                _ => { }));
    }

    [Fact]
    public async Task CreateHandler_TaskQueueCanComeFromEnvironment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TemporalLambdaWorker.CreateHandler(
                Version,
                _ => { },
                new TemporalLambdaWorkerHandlerOptions
                {
                    GetEnvironmentVariable = _ => null,
                }));

        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            config => config.ClientOptions.TargetHost = "localhost:7233",
            new TemporalLambdaWorkerHandlerOptions
            {
                GetEnvironmentVariable = name =>
                    name == "TEMPORAL_TASK_QUEUE" ? "env-task-queue" : null,
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = options;
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());
        Assert.NotNull(capturedWorkerOptions);
        Assert.Equal("env-task-queue", capturedWorkerOptions.TaskQueue);
    }

    [Fact]
    public async Task Invoke_SetsLambdaIdentityUnlessUserConfiguredIdentity()
    {
        TemporalClientConnectOptions? capturedClientOptions = null;
        var context = new FakeLambdaContext
        {
            AwsRequestId = "request-id",
            InvokedFunctionArn = "function-arn",
        };
        var handler = CreateCapturingHandler(
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "task-queue";
            },
            options => capturedClientOptions = options);

        await handler(null, context);

        Assert.NotNull(capturedClientOptions);
        Assert.Equal("request-id@function-arn", capturedClientOptions.Identity);

        handler = CreateCapturingHandler(
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.ClientOptions.Identity = "user-identity";
                config.WorkerOptions.TaskQueue = "task-queue";
            },
            options => capturedClientOptions = options);

        await handler(null, context);

        Assert.NotNull(capturedClientOptions);
        Assert.Equal("user-identity", capturedClientOptions.Identity);
    }

    [Fact]
    public async Task Invoke_DeadlineCancellationIsNormalAndRunsShutdownHooks()
    {
        var hookRan = false;
        CancellationToken workerToken = default;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "task-queue";
                config.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(10);
                config.ShutdownHooks.Add(_ =>
                {
                    hookRan = true;
                    return Task.CompletedTask;
                });
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, _) => new FakeLambdaWorker(async token =>
                {
                    workerToken = token;
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }),
            });

        await handler(null, new FakeLambdaContext { RemainingTime = TimeSpan.FromMilliseconds(40) });

        Assert.True(workerToken.IsCancellationRequested);
        Assert.True(hookRan);
    }

    [Fact]
    public async Task Invoke_RecomputesWorkerBudgetAfterSetupAndBeforeWorkerRun()
    {
        var context = new FakeLambdaContext(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromSeconds(1));
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "task-queue";
                config.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(10);
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ =>
                {
                    Assert.Equal(1, context.RemainingTimeReadCount);
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, _) =>
                {
                    Assert.Equal(1, context.RemainingTimeReadCount);
                    return new FakeLambdaWorker(async token =>
                    {
                        Assert.Equal(2, context.RemainingTimeReadCount);
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    });
                },
            });

        await handler(null, context);

        Assert.Equal(3, context.RemainingTimeReadCount);
    }

    [Fact]
    public async Task Invoke_TightDeadlinesThrowOrWarn()
    {
        var connectCalls = 0;
        var throwingHandler = TemporalLambdaWorker.CreateHandler(
            Version,
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "task-queue";
                config.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(100);
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ =>
                {
                    connectCalls++;
                    return Task.FromResult<object>(new object());
                },
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            throwingHandler(
                null,
                new FakeLambdaContext { RemainingTime = TimeSpan.FromMilliseconds(50) }));
        Assert.Equal(0, connectCalls);

        var warningContext = new FakeLambdaContext { RemainingTime = TimeSpan.FromMilliseconds(40) };
        var warningHandler = TemporalLambdaWorker.CreateHandler(
            Version,
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "task-queue";
                config.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(10);
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, _) => new FakeLambdaWorker(
                    token => Task.Delay(Timeout.InfiniteTimeSpan, token)),
            });

        await warningHandler(null, warningContext);

        Assert.Contains(
            warningContext.CaptureLogger.Lines,
            line => line.Contains("WARNING: Temporal Lambda worker budget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoke_ShutdownHooksRunInOrderPerInvocationAndContinueAfterFailures()
    {
        var hookCalls = new List<string>();
        var connectCalls = 0;
        var workerCreations = 0;
        var context = new FakeLambdaContext();
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            config =>
            {
                config.ClientOptions.TargetHost = "localhost:7233";
                config.WorkerOptions.TaskQueue = "task-queue";
                config.ShutdownHooks.Add(_ =>
                {
                    hookCalls.Add("first");
                    return Task.CompletedTask;
                });
                config.ShutdownHooks.Add(_ =>
                {
                    hookCalls.Add("second");
                    throw new InvalidOperationException("hook failed");
                });
                config.ShutdownHooks.Add(_ =>
                {
                    hookCalls.Add("third");
                    return Task.CompletedTask;
                });
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ =>
                {
                    connectCalls++;
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, _) =>
                {
                    workerCreations++;
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, context);
        await handler(null, context);

        Assert.Equal(
            new[] { "first", "second", "third", "first", "second", "third" },
            hookCalls);
        Assert.Equal(
            2,
            context.CaptureLogger.Lines.Count(
                line => line.Contains("shutdown hook failed", StringComparison.Ordinal)));
        Assert.Equal(2, connectCalls);
        Assert.Equal(2, workerCreations);
    }

    private static Func<object?, ILambdaContext, Task> CreateCapturingHandler(
        Action<LambdaWorkerConfig> configure,
        Action<TemporalClientConnectOptions> captureClientOptions) =>
        TemporalLambdaWorker.CreateHandler(
            Version,
            configure,
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = options =>
                {
                    captureClientOptions(options);
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, _) => new FakeLambdaWorker(_ => Task.CompletedTask),
            });

    private static int SimpleMaximum(PollerBehavior? behavior) =>
        Assert.IsType<PollerBehavior.SimpleMaximum>(behavior).Maximum;

    private static ActivityDefinition DummyActivity() =>
        ActivityDefinition.Create(
            "dummy",
            typeof(Task),
            Array.Empty<Type>(),
            0,
            _ => Task.CompletedTask);

    private sealed class FakeLambdaWorker : ILambdaWorker
    {
        private readonly Func<CancellationToken, Task> executeAsync;

        public FakeLambdaWorker(Func<CancellationToken, Task> executeAsync) =>
            this.executeAsync = executeAsync;

        public Task ExecuteAsync(CancellationToken stoppingToken) =>
            executeAsync(stoppingToken);

        public void Dispose()
        {
        }
    }

    private sealed class FakeLambdaContext : ILambdaContext
    {
        private readonly Queue<TimeSpan> remainingTimes = new();
        private TimeSpan remainingTime = TimeSpan.FromMinutes(1);

        public FakeLambdaContext()
        {
        }

        public FakeLambdaContext(params TimeSpan[] remainingTimes)
        {
            foreach (var remaining in remainingTimes)
            {
                this.remainingTimes.Enqueue(remaining);
            }
        }

        public CaptureLambdaLogger CaptureLogger { get; } = new();

        public string AwsRequestId { get; set; } = "request-id";

        public IClientContext ClientContext { get; } = null!;

        public string FunctionName { get; } = "function-name";

        public string FunctionVersion { get; } = "1";

        public ICognitoIdentity Identity { get; } = null!;

        public string InvokedFunctionArn { get; set; } = "function-arn";

        public ILambdaLogger Logger => CaptureLogger;

        public string LogGroupName { get; } = "log-group";

        public string LogStreamName { get; } = "log-stream";

        public int MemoryLimitInMB { get; } = 128;

        public int RemainingTimeReadCount { get; private set; }

        public TimeSpan RemainingTime
        {
            get
            {
                RemainingTimeReadCount++;
                if (remainingTimes.Count > 0)
                {
                    remainingTime = remainingTimes.Dequeue();
                }
                return remainingTime;
            }

            set
            {
                remainingTimes.Clear();
                remainingTime = value;
            }
        }
    }

    private sealed class CaptureLambdaLogger : ILambdaLogger
    {
        public List<string> Lines { get; } = new();

        public void Log(string message) => Lines.Add(message);

        public void LogLine(string message) => Lines.Add(message);
    }
}
