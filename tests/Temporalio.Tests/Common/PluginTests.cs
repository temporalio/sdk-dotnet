using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit.Abstractions;

namespace Temporalio.Tests.Common;

using System.Threading;
using Xunit;

public class PluginTests : WorkflowEnvironmentTestBase
{
    public PluginTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    private class ClientPlugin : ITemporalClientPlugin
    {
        public string Name => "ClientPlugin";

        public void ConfigureClient(TemporalClientOptions options)
        {
            options.Namespace = "NewNamespace";
        }

        public Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation) =>
            throw new NotImplementedException();
    }

    private class WorkerPlugin : ITemporalWorkerPlugin
    {
        public string Name => "WorkerPlugin";

        public void ConfigureWorker(TemporalWorkerOptions options)
        {
            options.TaskQueue = "NewTaskQueue";
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

    private class CombinedPlugin : WorkerPlugin, ITemporalClientPlugin
    {
        public new string Name => "CombinedPlugin";

        public void ConfigureClient(TemporalClientOptions options)
        {
            options.Namespace = "NewNamespace";
        }

        public Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation)
        {
            options.TargetHost = "Invalid";
            return continuation(options);
        }
    }

    [Fact]
    public void TestClientPlugin()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { new ClientPlugin() };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        Assert.Equal("NewNamespace", client.Options.Namespace);
    }

    private class FailToConnectPlugin : ITemporalClientPlugin
    {
        public string Name => "FailToConnectPlugin";

        public void ConfigureClient(TemporalClientOptions options)
        {
        }

        public Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation)
        {
            Assert.Equal("Invalid", options.TargetHost);
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task TestClientPlugin_Connect_Interceptor()
    {
        var options = new TemporalClientConnectOptions()
        {
            Plugins = new ITemporalClientPlugin[]
            {
                new ClientPlugin(),
                new FailToConnectPlugin(),
            },
        };

        // Test the interceptor is invoked, the second one is invoked second as the assert does not fail.
        await Assert.ThrowsAsync<NotImplementedException>(async () => await TemporalClient.ConnectAsync(options));
    }

#pragma warning disable CA1812
    [Workflow]
    private class SimpleWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public void TestWorkerPlugin()
    {
        using var worker = new TemporalWorker(Env.Client, new TemporalWorkerOptions()
        {
            Plugins = new[] { new CombinedPlugin() },
        }.AddWorkflow<SimpleWorkflow>());
        Assert.Equal("NewTaskQueue", worker.Options.TaskQueue);
    }

    [Fact]
    public void TestWorkerPlugin_Inheritance()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { new CombinedPlugin() };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        using var worker = new TemporalWorker(client, new TemporalWorkerOptions()
        {
            Plugins = new[] { new CombinedPlugin() },
        }.AddWorkflow<SimpleWorkflow>());
        Assert.Equal("NewTaskQueue", worker.Options.TaskQueue);
    }

    private class Codec : IPayloadCodec
    {
        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) => throw new NotImplementedException();
    }

    [Fact]
    public void TestSimplePlugin_Basic()
    {
        var plugin = new SimplePlugin("SimplePlugin", new SimplePluginOptions()
        {
            DataConverter = new DataConverter(new DefaultPayloadConverter(), new DefaultFailureConverter(), new Codec()),
        }.AddWorkflow<SimpleWorkflow>());
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { plugin };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        Assert.NotNull(client.Options.DataConverter.PayloadCodec);

        using var worker = new TemporalWorker(client, new TemporalWorkerOptions()
        {
            TaskQueue = "TestSimplePlugin_Basic",
        });
        Assert.NotNull(client.Options.DataConverter.PayloadCodec);
    }

    [Fact]
    public void TestSimplePlugin_Function()
    {
        var plugin = new SimplePlugin("SimplePlugin", new SimplePluginOptions()
        {
            DataConverterOption = new SimplePluginOptions.SimplePluginOption<DataConverter>(
                (converter) => converter with { PayloadCodec = new Codec() }),
        }.AddWorkflow<SimpleWorkflow>());
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { plugin };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        Assert.NotNull(client.Options.DataConverter.PayloadCodec);

        using var worker = new TemporalWorker(client, new TemporalWorkerOptions()
        {
            TaskQueue = "TestSimplePlugin_Function",
        });
        Assert.NotNull(client.Options.DataConverter.PayloadCodec);
    }

    [Fact]
    public async Task TestSimplePlugin_RunContext()
    {
        List<string> transitions = new();
        var plugin = new SimplePlugin("SimplePlugin", new SimplePluginOptions()
        {
            RunContextBefore = async () => transitions.Add("Beginning"),
            RunContextAfter = async () => transitions.Add("Ending"),
        }.AddWorkflow<SimpleWorkflow>());
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { plugin };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        using var worker = new TemporalWorker(client, new TemporalWorkerOptions()
        {
            TaskQueue = "TestSimplePlugin_Function",
        });
        using var cancelToken = new CancellationTokenSource();
        var execution = worker.ExecuteAsync(cancelToken.Token);
        cancelToken.CancelAfter(500);
        try
        {
            await execution;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains("Beginning", transitions);
        Assert.Contains("Ending", transitions);
    }
}