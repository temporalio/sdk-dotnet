using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit.Abstractions;

namespace Temporalio.Tests.Common;

using Xunit;

public class PluginTests : WorkflowEnvironmentTestBase
{
    public PluginTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    private class ClientPlugin : ITemporalClientPlugin
    {
        public virtual string Name => "ClientPlugin";

        public void ConfigureClient(TemporalClientOptions options)
        {
            options.Namespace = "NewNamespace";
        }

        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options, Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation) => throw new NotImplementedException();
    }

    private class WorkerPlugin : ITemporalWorkerPlugin
    {
        public string Name => "WorkerPlugin";

        public void ConfigureWorker(TemporalWorkerOptions options)
        {
            options.TaskQueue = "NewTaskQueue";
        }

        public Task RunWorkerAsync(TemporalWorker worker, Func<TemporalWorker, Task> continuation) => throw new NotImplementedException();

        public void ConfigureReplayer(WorkflowReplayerOptions options) => throw new NotImplementedException();

        public Task<IEnumerable<WorkflowReplayResult>> RunReplayerAsync(WorkflowReplayer replayer, Func<WorkflowReplayer, Task<IEnumerable<WorkflowReplayResult>>> continuation) => throw new NotImplementedException();
    }

    private class CombinedPlugin : WorkerPlugin, ITemporalClientPlugin
    {
        public new string Name => "CombinedPlugin";

        public void ConfigureClient(TemporalClientOptions options)
        {
            options.Namespace = "NewNamespace";
        }

        public Task<TemporalConnection> TemporalConnectAsync(TemporalClientConnectOptions options, Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation) => throw new NotImplementedException();
    }

    [Fact]
    public void TestClientPlugin()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { new ClientPlugin() };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        Assert.Equal("NewNamespace", client.Options.Namespace);
    }

    [Fact]
    public void TestWorkerPlugin()
    {
        using var worker = new TemporalWorker(Env.Client, new TemporalWorkerOptions()
        {
            Plugins = new[] { new CombinedPlugin() },
        });
        Assert.Equal("NewTaskQueue", worker.Options.TaskQueue);
    }

    [Fact]
    public void TestWorkerPlugin_Inheritance()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { new ClientPlugin() };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        Assert.Equal("NewNamespace", client.Options.Namespace);
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
            Activities = new List<ActivityDefinition>(),
            Workflows = new List<WorkflowDefinition>(),
            NexusServices = new List<ServiceHandlerInstance>(),
            DataConverter = new DataConverter(new DefaultPayloadConverter(), new DefaultFailureConverter(), new Codec()),
        });
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new[] { plugin };

        var client = new TemporalClient(Env.Client.Connection, newOptions);
        Assert.NotNull(client.Options.DataConverter.PayloadCodec);
    }
}