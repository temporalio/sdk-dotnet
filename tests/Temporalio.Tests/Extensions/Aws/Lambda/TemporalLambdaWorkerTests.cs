namespace Temporalio.Tests.Extensions.Aws.Lambda;

using Amazon.Lambda.Core;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Aws.Lambda;
using Temporalio.Worker;
using Temporalio.Worker.Tuning;
using Temporalio.Workflows;
using Xunit;

[Collection(TemporalLambdaWorkerNonParallelDefinition.Name)]
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
            options =>
            {
                configureCalls++;
                Assert.Equal(2, options.WorkerOptions.MaxConcurrentActivities);
                Assert.Equal(10, options.WorkerOptions.MaxConcurrentWorkflowTasks);
                Assert.Equal(2, options.WorkerOptions.MaxConcurrentLocalActivities);
                Assert.Equal(5, options.WorkerOptions.MaxConcurrentNexusTasks);
                Assert.Equal(TimeSpan.FromSeconds(5), options.WorkerOptions.GracefulShutdownTimeout);
                Assert.Equal(30, options.WorkerOptions.MaxCachedWorkflows);
                Assert.Equal(2, options.WorkerOptions.MaxConcurrentWorkflowTaskPolls);
                Assert.Equal(1, options.WorkerOptions.MaxConcurrentActivityTaskPolls);
                Assert.Equal(1, options.WorkerOptions.MaxConcurrentNexusTaskPolls);
                Assert.Null(options.WorkerOptions.WorkflowTaskPollerBehavior);
                Assert.Null(options.WorkerOptions.ActivityTaskPollerBehavior);
                Assert.Null(options.WorkerOptions.NexusTaskPollerBehavior);
                Assert.True(options.WorkerOptions.DisableEagerActivityExecution);
                Assert.NotNull(options.WorkerOptions.DeploymentOptions);
                Assert.Equal(Version, options.WorkerOptions.DeploymentOptions.Version);
                Assert.True(options.WorkerOptions.DeploymentOptions.UseWorkerVersioning);
                Assert.Equal(
                    VersioningBehavior.AutoUpgrade,
                    options.WorkerOptions.DeploymentOptions.DefaultVersioningBehavior);
                Assert.Equal("env-task-queue", options.WorkerOptions.TaskQueue);
                Assert.Equal("loaded-address", options.ClientOptions.TargetHost);
                Assert.Equal("loaded-namespace", options.ClientOptions.Namespace);

                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "configured-task-queue";
                options.WorkerOptions.MaxConcurrentActivities = 8;
                options.WorkerOptions.MaxConcurrentActivityTaskPolls = 4;
                options.WorkerOptions.MaxCachedWorkflows = 12;
                options.WorkerOptions.DisableEagerActivityExecution = false;
                options.WorkerOptions.DeploymentOptions = new WorkerDeploymentOptions(
                    new WorkerDeploymentVersion("ignored", "ignored"),
                    useWorkerVersioning: false)
                {
                    DefaultVersioningBehavior = VersioningBehavior.Pinned,
                };
                options.WorkerOptions.Activities.Add(DummyActivity());
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                LoadClientConnectOptions = _ => new TemporalClientConnectOptions
                {
                    TargetHost = "loaded-address",
                    Namespace = "loaded-namespace",
                },
                GetEnvironmentVariable = name =>
                    name == "TEMPORAL_TASK_QUEUE" ? "env-task-queue" : null,
                ConnectClientAsync = options =>
                {
                    capturedClientOptions = options;
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = SimulateWorkerPluginConfiguration(options);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        Assert.Equal(0, configureCalls);
        await handler(null, new FakeLambdaContext());

        Assert.Equal(1, configureCalls);
        Assert.NotNull(capturedClientOptions);
        Assert.NotNull(capturedWorkerOptions);
        Assert.Equal("localhost:7233", capturedClientOptions.TargetHost);
        Assert.Equal("loaded-namespace", capturedClientOptions.Namespace);
        Assert.Equal("configured-task-queue", capturedWorkerOptions.TaskQueue);
        Assert.Equal(8, capturedWorkerOptions.MaxConcurrentActivities);
        Assert.Equal(10, capturedWorkerOptions.MaxConcurrentWorkflowTasks);
        Assert.Equal(2, capturedWorkerOptions.MaxConcurrentLocalActivities);
        Assert.Equal(5, capturedWorkerOptions.MaxConcurrentNexusTasks);
        Assert.Equal(2, capturedWorkerOptions.MaxConcurrentWorkflowTaskPolls);
        Assert.Equal(4, capturedWorkerOptions.MaxConcurrentActivityTaskPolls);
        Assert.Equal(1, capturedWorkerOptions.MaxConcurrentNexusTaskPolls);
        Assert.Null(capturedWorkerOptions.WorkflowTaskPollerBehavior);
        Assert.Null(capturedWorkerOptions.ActivityTaskPollerBehavior);
        Assert.Null(capturedWorkerOptions.NexusTaskPollerBehavior);
        Assert.Equal(12, capturedWorkerOptions.MaxCachedWorkflows);
        Assert.False(capturedWorkerOptions.DisableEagerActivityExecution);
        Assert.NotNull(capturedWorkerOptions.DeploymentOptions);
        Assert.Equal(Version, capturedWorkerOptions.DeploymentOptions.Version);
        Assert.True(capturedWorkerOptions.DeploymentOptions.UseWorkerVersioning);
        Assert.Equal(
            VersioningBehavior.Pinned,
            capturedWorkerOptions.DeploymentOptions.DefaultVersioningBehavior);
#pragma warning disable CS0618 // Verifying the Lambda helper clears legacy versioning options.
        Assert.Null(capturedWorkerOptions.BuildId);
        Assert.False(capturedWorkerOptions.UseWorkerVersioning);
#pragma warning restore CS0618
    }

    [Fact]
    public void AddShutdownHook_NullHook_Throws()
    {
        var options = new TemporalLambdaWorkerOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddShutdownHook(null!));
    }

    [Fact]
    public async Task CreateHandler_DefaultsVersioningBehaviorToAutoUpgrade()
    {
        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.WorkerOptions.AddWorkflow<WorkflowWithoutVersioningBehavior>();
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = SimulateWorkerPluginConfiguration(options);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());

        Assert.NotNull(capturedWorkerOptions);
        Assert.NotNull(capturedWorkerOptions.DeploymentOptions);
        Assert.Equal(Version, capturedWorkerOptions.DeploymentOptions.Version);
        Assert.True(capturedWorkerOptions.DeploymentOptions.UseWorkerVersioning);
        Assert.Equal(
            VersioningBehavior.AutoUpgrade,
            capturedWorkerOptions.DeploymentOptions.DefaultVersioningBehavior);
    }

    [Fact]
    public async Task CreateHandler_LoadsDefaultClientOptionsWhenNotOverridden()
    {
        var loadCalls = 0;
        TemporalClientConnectOptions? capturedClientOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.WorkerOptions.TaskQueue = "task-queue";
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                LoadClientConnectOptions = _ =>
                {
                    loadCalls++;
                    return new TemporalClientConnectOptions
                    {
                        TargetHost = "loaded-address",
                        Namespace = "loaded-namespace",
                    };
                },
                ConnectClientAsync = options =>
                {
                    capturedClientOptions = options;
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, _) => new FakeLambdaWorker(_ => Task.CompletedTask),
            });

        await handler(null, new FakeLambdaContext());

        Assert.Equal(1, loadCalls);
        Assert.NotNull(capturedClientOptions);
        Assert.Equal("loaded-address", capturedClientOptions.TargetHost);
        Assert.Equal("loaded-namespace", capturedClientOptions.Namespace);
    }

    [Fact]
    public async Task CreateHandler_ExplicitClientOptionsBypassDefaultConfigLoad()
    {
        TemporalClientConnectOptions? capturedClientOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.ClientOptions = new TemporalClientConnectOptions
                {
                    TargetHost = "explicit-address",
                    Namespace = "explicit-namespace",
                };
                options.WorkerOptions.TaskQueue = "task-queue";
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                LoadClientConnectOptions = _ =>
                    throw new InvalidOperationException("Config should not be loaded"),
                ConnectClientAsync = options =>
                {
                    capturedClientOptions = options;
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, _) => new FakeLambdaWorker(_ => Task.CompletedTask),
            });

        await handler(null, new FakeLambdaContext());

        Assert.NotNull(capturedClientOptions);
        Assert.Equal("explicit-address", capturedClientOptions.TargetHost);
        Assert.Equal("explicit-namespace", capturedClientOptions.Namespace);
    }

    [Fact]
    public async Task CreateHandler_ClearsConcurrencyDefaultsWhenTunerSet()
    {
        var tuner = WorkerTuner.CreateFixedSize(
            workflowTaskSlots: 1,
            activityTaskSlots: 2,
            localActivitySlots: 3,
            nexusTaskSlots: 4);
        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.WorkerOptions.Tuner = tuner;
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = SimulateWorkerPluginConfiguration(options);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());

        Assert.NotNull(capturedWorkerOptions);
        Assert.Same(tuner, capturedWorkerOptions.Tuner);
        Assert.Null(capturedWorkerOptions.MaxConcurrentActivities);
        Assert.Null(capturedWorkerOptions.MaxConcurrentWorkflowTasks);
        Assert.Null(capturedWorkerOptions.MaxConcurrentLocalActivities);
        Assert.Null(capturedWorkerOptions.MaxConcurrentNexusTasks);
    }

    [Fact]
    public async Task CreateHandler_ClearsConcurrencyDefaultsWhenPluginSetsTuner()
    {
        var tuner = WorkerTuner.CreateFixedSize(
            workflowTaskSlots: 1,
            activityTaskSlots: 2,
            localActivitySlots: 3,
            nexusTaskSlots: 4);
        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.WorkerOptions.Plugins = new[] { new TunerPlugin(tuner) };
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = SimulateWorkerPluginConfiguration(options);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());

        Assert.NotNull(capturedWorkerOptions);
        Assert.Same(tuner, capturedWorkerOptions.Tuner);
        Assert.Null(capturedWorkerOptions.MaxConcurrentActivities);
        Assert.Null(capturedWorkerOptions.MaxConcurrentWorkflowTasks);
        Assert.Null(capturedWorkerOptions.MaxConcurrentLocalActivities);
        Assert.Null(capturedWorkerOptions.MaxConcurrentNexusTasks);
    }

    [Fact]
    public async Task CreateHandler_ReappliesDeploymentVersionAfterPlugins()
    {
        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.WorkerOptions.Plugins = new[] { new VersioningPlugin() };
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, options) =>
                {
                    capturedWorkerOptions = SimulateWorkerPluginConfiguration(options);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());

        Assert.NotNull(capturedWorkerOptions);
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
        var missingTaskQueueHandler = TemporalLambdaWorker.CreateHandler(
            Version,
            _ => { },
            new TemporalLambdaWorkerHandlerOptions
            {
                GetEnvironmentVariable = _ => null,
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            missingTaskQueueHandler(null, new FakeLambdaContext()));

        TemporalWorkerOptions? capturedWorkerOptions = null;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options => options.ClientOptions.TargetHost = "localhost:7233",
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
    public void LoadClientConnectOptions_ExplicitConfigSourceWins()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var envConfigPath = Path.Combine(tempDir, "env.toml");
            File.WriteAllText(envConfigPath, ConfigToml("env-address", "env-namespace"));

            var options = TemporalLambdaWorker.LoadClientConnectOptions(
                new ClientEnvConfig.ProfileLoadOptions
                {
                    ConfigSource = DataSource.FromUTF8String(
                        ConfigToml("explicit-address", "explicit-namespace")),
                    OverrideEnvVars = new Dictionary<string, string>
                    {
                        ["TEMPORAL_CONFIG_FILE"] = envConfigPath,
                    },
                });

            Assert.Equal("explicit-address", options.TargetHost);
            Assert.Equal("explicit-namespace", options.Namespace);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadClientConnectOptions_TemporalConfigFileWinsOverLambdaTaskRoot()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var envConfigPath = Path.Combine(tempDir, "env.toml");
            File.WriteAllText(envConfigPath, ConfigToml("env-address", "env-namespace"));
            var lambdaRoot = Path.Combine(tempDir, "lambda-root");
            Directory.CreateDirectory(lambdaRoot);
            File.WriteAllText(
                Path.Combine(lambdaRoot, "temporal.toml"),
                ConfigToml("lambda-address", "lambda-namespace"));

            var options = TemporalLambdaWorker.LoadClientConnectOptions(
                new ClientEnvConfig.ProfileLoadOptions
                {
                    OverrideEnvVars = new Dictionary<string, string>
                    {
                        ["TEMPORAL_CONFIG_FILE"] = envConfigPath,
                        ["LAMBDA_TASK_ROOT"] = lambdaRoot,
                    },
                });

            Assert.Equal("env-address", options.TargetHost);
            Assert.Equal("env-namespace", options.Namespace);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadClientConnectOptions_UsesLambdaTaskRootTemporalToml()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "temporal.toml"),
                ConfigToml("lambda-address", "lambda-namespace"));

            var options = TemporalLambdaWorker.LoadClientConnectOptions(
                new ClientEnvConfig.ProfileLoadOptions
                {
                    OverrideEnvVars = new Dictionary<string, string>
                    {
                        ["LAMBDA_TASK_ROOT"] = tempDir,
                    },
                });

            Assert.Equal("lambda-address", options.TargetHost);
            Assert.Equal("lambda-namespace", options.Namespace);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadClientConnectOptions_FallsBackToCurrentDirectoryTemporalToml()
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "temporal.toml"),
                ConfigToml("cwd-address", "cwd-namespace"));
            Directory.SetCurrentDirectory(tempDir);

            var options = TemporalLambdaWorker.LoadClientConnectOptions(
                new ClientEnvConfig.ProfileLoadOptions
                {
                    OverrideEnvVars = new Dictionary<string, string>(),
                });

            Assert.Equal("cwd-address", options.TargetHost);
            Assert.Equal("cwd-namespace", options.Namespace);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadClientConnectOptions_MissingLambdaConfigAllowsEnvOnly()
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        var tempDir = CreateTempDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var options = TemporalLambdaWorker.LoadClientConnectOptions(
                new ClientEnvConfig.ProfileLoadOptions
                {
                    OverrideEnvVars = new Dictionary<string, string>
                    {
                        ["TEMPORAL_ADDRESS"] = "env-only-address",
                        ["TEMPORAL_NAMESPACE"] = "env-only-namespace",
                    },
                });

            Assert.Equal("env-only-address", options.TargetHost);
            Assert.Equal("env-only-namespace", options.Namespace);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Directory.Delete(tempDir, recursive: true);
        }
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
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
            },
            options => capturedClientOptions = options);

        await handler(null, context);

        Assert.NotNull(capturedClientOptions);
        Assert.Equal("request-id@function-arn", capturedClientOptions.Identity);

        handler = CreateCapturingHandler(
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.ClientOptions.Identity = "user-identity";
                options.WorkerOptions.TaskQueue = "task-queue";
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
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(10);
                options.AddShutdownHook(_ =>
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
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(10);
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
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(100);
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
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(10);
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
            options =>
            {
                options.ClientOptions.TargetHost = "localhost:7233";
                options.WorkerOptions.TaskQueue = "task-queue";
                options.AddShutdownHook(_ =>
                {
                    hookCalls.Add("first");
                    return Task.CompletedTask;
                });
                options.AddShutdownHook(_ =>
                {
                    hookCalls.Add("second");
                    throw new InvalidOperationException("hook failed");
                });
                options.AddShutdownHook(_ =>
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

    [Fact]
    public async Task CreateHandler_SyncConfigureRunsPerInvocationWithFreshConfig()
    {
        var configureCalls = 0;
        var capturedConfigs = new List<TemporalLambdaWorkerOptions>();
        var capturedTargets = new List<string?>();
        var capturedTaskQueues = new List<string?>();
        var hookCalls = new List<string>();
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                var call = ++configureCalls;
                capturedConfigs.Add(options);
                Assert.Equal("env-task-queue", options.WorkerOptions.TaskQueue);

                options.ClientOptions.TargetHost = $"target-{call}";
                options.WorkerOptions.TaskQueue = $"task-queue-{call}";
                options.AddShutdownHook(_ =>
                {
                    hookCalls.Add($"hook-{call}");
                    return Task.CompletedTask;
                });
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                GetEnvironmentVariable = name =>
                    name == "TEMPORAL_TASK_QUEUE" ? "env-task-queue" : null,
                ConnectClientAsync = options =>
                {
                    capturedTargets.Add(options.TargetHost);
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, options) =>
                {
                    capturedTaskQueues.Add(options.TaskQueue);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());
        await handler(null, new FakeLambdaContext());

        Assert.Equal(2, configureCalls);
        Assert.Equal(2, capturedConfigs.Count);
        Assert.NotSame(capturedConfigs[0], capturedConfigs[1]);
        Assert.Equal(new[] { "target-1", "target-2" }, capturedTargets);
        Assert.Equal(new[] { "task-queue-1", "task-queue-2" }, capturedTaskQueues);
        Assert.Equal(new[] { "hook-1", "hook-2" }, hookCalls);
    }

    [Fact]
    public async Task CreateHandler_AsyncConfigureRunsPerInvocationWithFreshConfig()
    {
        var configureCalls = 0;
        var capturedConfigs = new List<TemporalLambdaWorkerOptions>();
        var capturedTargets = new List<string?>();
        var capturedTaskQueues = new List<string?>();
        var hookCalls = new List<string>();
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            async options =>
            {
                await Task.Yield();
                var call = ++configureCalls;
                capturedConfigs.Add(options);
                Assert.Equal("env-task-queue", options.WorkerOptions.TaskQueue);

                options.ClientOptions.TargetHost = $"target-{call}";
                options.WorkerOptions.TaskQueue = $"task-queue-{call}";
                options.AddShutdownHook(_ =>
                {
                    hookCalls.Add($"hook-{call}");
                    return Task.CompletedTask;
                });
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                GetEnvironmentVariable = name =>
                    name == "TEMPORAL_TASK_QUEUE" ? "env-task-queue" : null,
                ConnectClientAsync = options =>
                {
                    capturedTargets.Add(options.TargetHost);
                    return Task.FromResult<object>(new object());
                },
                CreateWorker = (_, options) =>
                {
                    capturedTaskQueues.Add(options.TaskQueue);
                    return new FakeLambdaWorker(_ => Task.CompletedTask);
                },
            });

        await handler(null, new FakeLambdaContext());
        await handler(null, new FakeLambdaContext());

        Assert.Equal(2, configureCalls);
        Assert.Equal(2, capturedConfigs.Count);
        Assert.NotSame(capturedConfigs[0], capturedConfigs[1]);
        Assert.Equal(new[] { "target-1", "target-2" }, capturedTargets);
        Assert.Equal(new[] { "task-queue-1", "task-queue-2" }, capturedTaskQueues);
        Assert.Equal(new[] { "hook-1", "hook-2" }, hookCalls);
    }

    [Fact]
    public async Task CreateHandler_AsyncConfigureErrorsSurfaceOnInvocation()
    {
        var configureCalls = 0;
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            async options =>
            {
                _ = options;
                await Task.Yield();
                configureCalls++;
                throw new InvalidOperationException("bad options");
            },
            new TemporalLambdaWorkerHandlerOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler(null, new FakeLambdaContext()));
        Assert.Equal("bad options", error.Message);
        Assert.Equal(1, configureCalls);
    }

    [Fact]
    public async Task CreateHandler_AsyncConfigureValidatesTaskQueueOnInvocation()
    {
        var handler = TemporalLambdaWorker.CreateHandler(
            Version,
            _ => Task.CompletedTask,
            new TemporalLambdaWorkerHandlerOptions
            {
                GetEnvironmentVariable = _ => null,
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler(null, new FakeLambdaContext()));
    }

    [Fact]
    public async Task CreateHandler_AsyncConfigureShutdownHooksRunAfterFailures()
    {
        var hookCalls = new List<string>();
        var connectFailureHandler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.WorkerOptions.TaskQueue = "task-queue";
                options.AddShutdownHook(_ =>
                {
                    hookCalls.Add("connect");
                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ =>
                    throw new InvalidOperationException("connect failed"),
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connectFailureHandler(null, new FakeLambdaContext()));

        var workerFailureHandler = TemporalLambdaWorker.CreateHandler(
            Version,
            options =>
            {
                options.WorkerOptions.TaskQueue = "task-queue";
                options.AddShutdownHook(_ =>
                {
                    hookCalls.Add("worker");
                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            },
            new TemporalLambdaWorkerHandlerOptions
            {
                ConnectClientAsync = _ => Task.FromResult<object>(new object()),
                CreateWorker = (_, _) => new FakeLambdaWorker(
                    _ => throw new InvalidOperationException("worker failed")),
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workerFailureHandler(null, new FakeLambdaContext()));

        Assert.Equal(new[] { "connect", "worker" }, hookCalls);
    }

    private static Func<object?, ILambdaContext, Task> CreateCapturingHandler(
        Action<TemporalLambdaWorkerOptions> configure,
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

    private static string ConfigToml(string address, string nameSpace) => $@"
[profile.default]
address = ""{address}""
namespace = ""{nameSpace}""
";

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            $"TemporalLambdaWorkerTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static ActivityDefinition DummyActivity() =>
        ActivityDefinition.Create(
            "dummy",
            typeof(Task),
            Array.Empty<Type>(),
            0,
            _ => Task.CompletedTask);

    private static TemporalWorkerOptions SimulateWorkerPluginConfiguration(
        TemporalWorkerOptions options)
    {
        var plugins = new List<ITemporalWorkerPlugin>(
            options.Plugins ?? Array.Empty<ITemporalWorkerPlugin>());
        foreach (var plugin in plugins)
        {
            plugin.ConfigureWorker(options);
        }
        return options;
    }

    [Workflow]
    public sealed class WorkflowWithoutVersioningBehavior
    {
        [WorkflowRun]
        public Task RunAsync() => Task.CompletedTask;
    }

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

    private sealed class TunerPlugin : ITemporalWorkerPlugin
    {
        private readonly WorkerTuner tuner;

        public TunerPlugin(WorkerTuner tuner) => this.tuner = tuner;

        public string Name => "TunerPlugin";

        public void ConfigureWorker(TemporalWorkerOptions options) => options.Tuner = tuner;

        public Task<TResult> RunWorkerAsync<TResult>(
            TemporalWorker worker,
            Func<TemporalWorker, CancellationToken, Task<TResult>> continuation,
            CancellationToken stoppingToken) =>
            throw new NotImplementedException();

        public void ConfigureReplayer(WorkflowReplayerOptions options) =>
            throw new NotImplementedException();

        public Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, CancellationToken, Task<IEnumerable<WorkflowReplayResult>>> continuation,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<WorkflowReplayResult> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, IAsyncEnumerable<WorkflowReplayResult>> continuation,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class VersioningPlugin : ITemporalWorkerPlugin
    {
        public string Name => "VersioningPlugin";

        public void ConfigureWorker(TemporalWorkerOptions options)
        {
            options.DeploymentOptions = new WorkerDeploymentOptions(
                new WorkerDeploymentVersion("plugin-deployment", "plugin-build"),
                useWorkerVersioning: false)
            {
                DefaultVersioningBehavior = VersioningBehavior.AutoUpgrade,
            };
#pragma warning disable CS0618 // Verifying the Lambda helper clears legacy versioning options.
            options.BuildId = "legacy-build";
            options.UseWorkerVersioning = true;
#pragma warning restore CS0618
        }

        public Task<TResult> RunWorkerAsync<TResult>(
            TemporalWorker worker,
            Func<TemporalWorker, CancellationToken, Task<TResult>> continuation,
            CancellationToken stoppingToken) =>
            throw new NotImplementedException();

        public void ConfigureReplayer(WorkflowReplayerOptions options) =>
            throw new NotImplementedException();

        public Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, CancellationToken, Task<IEnumerable<WorkflowReplayResult>>> continuation,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<WorkflowReplayResult> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, IAsyncEnumerable<WorkflowReplayResult>> continuation,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
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
