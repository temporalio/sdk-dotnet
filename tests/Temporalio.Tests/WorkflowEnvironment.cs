#pragma warning disable CA1001 // IAsyncLifetime is substitute for IAsyncDisposable here

namespace Temporalio.Tests;

using System;
using System.Diagnostics;
using Temporalio.Client;
using Xunit;

public class WorkflowEnvironment : IAsyncLifetime
{
    private readonly Lazy<KitchenSinkWorker> kitchenSinkWorker;
    private Temporalio.Testing.WorkflowEnvironment? env;

    public WorkflowEnvironment()
    {
        kitchenSinkWorker = new(StartKitchenSinkWorker);
    }

    public ITemporalClient Client =>
        env?.Client ?? throw new InvalidOperationException("Environment not created");

    public string KitchenSinkWorkerTaskQueue => kitchenSinkWorker.Value.TaskQueue;

    public async Task InitializeAsync()
    {
        // If an existing target is given via environment variable, use that
        if (Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_TARGET_HOST") is string host)
        {
            var options = new TemporalClientConnectOptions(host)
            {
                Namespace = Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_NAMESPACE") ??
                    throw new InvalidOperationException("Missing test namespace"),
            };
            var clientCert = Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_CERT");
            var clientKey = Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_KEY");
            if ((clientCert == null) != (clientKey == null))
            {
                throw new InvalidOperationException("Must have both cert/key or neither");
            }
            if (clientCert != null && clientKey != null)
            {
                options.Tls = new()
                {
                    ClientCert = System.Text.Encoding.ASCII.GetBytes(clientCert),
                    ClientPrivateKey = System.Text.Encoding.ASCII.GetBytes(clientKey),
                };
            }
            env = new(await Temporalio.Client.TemporalClient.ConnectAsync(options));
        }
        else
        {
            // Otherwise, local server is good
            env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(new()
            {
                Temporalite = new()
                {
                    ExtraArgs = new List<string>
                    {
                        // Disable search attribute cache
                        "--dynamic-config-value",
                        "system.forceSearchAttributesCacheRefreshOnRead=true",
                    },
                },
            });
        }
    }

    public async Task DisposeAsync()
    {
        if (kitchenSinkWorker.IsValueCreated)
        {
            kitchenSinkWorker.Value.Process.Kill();
            Assert.True(kitchenSinkWorker.Value.Process.WaitForExit(5000));
        }
        if (env != null)
        {
            await env.ShutdownAsync();
        }
    }

    private KitchenSinkWorker StartKitchenSinkWorker()
    {
        // Build
        var workerDir = Path.Join(
          Path.GetDirectoryName(TestUtils.CallerFilePath())!, "../golangworker");
        var exePath = Path.Join(workerDir, "golangworker");
        var proc = Process.Start(new ProcessStartInfo("go")
        {
            ArgumentList = { "build", "-o", exePath, "." },
            WorkingDirectory = workerDir,
        });
        proc!.WaitForExit();
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException("Go build failed");
        }

        // Start
        var taskQueue = Guid.NewGuid().ToString();
        proc = Process.Start(
            exePath,
            new string[]
            {
                Client.Connection.Options.TargetHost!,
                Client.Options.Namespace,
                taskQueue,
            });
        return new(proc, taskQueue);
    }

    private record KitchenSinkWorker(Process Process, string TaskQueue);
}
