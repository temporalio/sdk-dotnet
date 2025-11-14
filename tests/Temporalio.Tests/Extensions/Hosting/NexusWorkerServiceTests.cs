using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Extensions.Hosting;

public class NexusWorkerServiceTests : WorkflowEnvironmentTestBase
{
    public NexusWorkerServiceTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    public interface ITestCounterService :
        IDisposable
    {
        int Increment();
    }

    public sealed class TestCounterService : ITestCounterService
    {
        private bool isDisposed;
        private int value;

        public TestCounterService()
        {
        }

        public int Increment()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(TestCounterService));
            }
            return ++value;
        }

        public void Dispose()
        {
            isDisposed = true;
        }
    }

    [NexusService]
    public interface ITestNexusService
    {
        [NexusOperation]
        int Increment(string unused);
    }

    [NexusServiceHandler(typeof(ITestNexusService))]
    public class TestNexusService
    {
        private readonly ITestCounterService context;

        public TestNexusService(ITestCounterService context) =>
            this.context = context;

        [NexusOperationHandler]
        public IOperationHandler<string, int> Increment() =>
            new TestOperationHandler(this.context);

        private class TestOperationHandler : IOperationHandler<string, int>
        {
            private readonly ITestCounterService context;

            public TestOperationHandler(ITestCounterService context)
            {
                this.context = context;
            }

            public Task<OperationStartResult<int>> StartAsync(OperationStartContext context, string input) =>
                Task.FromResult(OperationStartResult.SyncResult(this.context.Increment()));

            public Task CancelAsync(OperationCancelContext context) =>
                throw new NotImplementedException();

            public Task<int> FetchResultAsync(OperationFetchResultContext context) =>
                throw new NotImplementedException();

            public Task<OperationInfo> FetchInfoAsync(OperationFetchInfoContext context) =>
                throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task NexusWorkerService_SingletonNexusService_SingletonDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddSingleton<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddSingletonOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 2 since both the Nexus service and dependency are singleton and dependency is shared across calls
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task NexusWorkerService_SingletonNexusService_ScopedDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddScoped<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddSingletonOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect to throw exception since scoped dependency cannot be used in singleton service
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task NexusWorkerService_SingletonNexusService_TransientDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddTransient<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddSingletonOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 2 since both the Nexus service is singleton and should hold onto its dependencies
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task NexusWorkerService_ScopedNexusService_SingletonDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddSingleton<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddScopedOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 2 since the dependency is singleton and is shared across calls
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task NexusWorkerService_ScopedNexusService_ScopedDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddScoped<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddScopedOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 1 since the dependency is scoped and disposed after the first call
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NexusWorkerService_ScopedNexusService_TransientDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddTransient<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddScopedOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 1 since the nexus service is scoped and gets a new transient dependency for each call
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NexusWorkerService_TransientNexusService_SingletonDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddSingleton<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddTransientOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 2 since the dependency is singleton and is shared across calls
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task NexusWorkerService_TransientNexusService_ScopedDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddScoped<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddTransientOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 1 since the dependency is scoped and disposed after the first call
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NexusWorkerService_TransientNexusService_TransientDependency()
    {
        int result = await ExecuteHostedNexusWithWorkflow(
            configureServices: services => services.AddTransient<ITestCounterService, TestCounterService>(),
            configureBuilder: builder => builder.AddTransientOperations<TestNexusService>(),
            workflowFunc: async client =>
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
                return await client.ExecuteNexusOperationAsync(svc => svc.Increment("unused"));
            });

        // Expect 1 since the nexus service and dependency are both transient and new instances are created for each call
        Assert.Equal(1, result);
    }

    private async Task<int> ExecuteHostedNexusWithWorkflow(
        Action<IServiceCollection> configureServices,
        Action<ITemporalWorkerServiceOptionsBuilder> configureBuilder,
        Func<NexusClient<ITestNexusService>, Task<int>> workflowFunc)
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var endpointName = $"nexus-endpoint-{taskQueue}";

        Func<Task<int>> outerWorkflowFunc = async () =>
        {
            var client = Workflow.CreateNexusClient<ITestNexusService>(endpointName);

            return await workflowFunc(client);
        };

        var builder = Host.CreateApplicationBuilder();

        var services = builder.Services.
            AddSingleton(Client);
        configureServices(services);

        var workerBuilder = services.
            AddHostedTemporalWorker(taskQueue).
            ConfigureOptions(options =>
                {
                    options.AddWorkflow(WorkflowDefinition.Create(
                        typeof(CustomFuncWorkflow),
                        null,
                        _ => new CustomFuncWorkflow(outerWorkflowFunc)));
                });
        configureBuilder(workerBuilder);

        using var host = builder.Build();

        await host.StartAsync();

        try
        {
            var endpoint = await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

            try
            {
                ITemporalClient client = host.Services.GetRequiredService<ITemporalClient>();

                var handle = await client.StartWorkflowAsync(
                    (CustomFuncWorkflow wf) => wf.RunAsync(),
                    new WorkflowOptions(
                        $"wf-{Guid.NewGuid()}",
                        taskQueue));

                return await handle.GetResultAsync<int>();
            }
            finally
            {
                await Env.TestEnv.DeleteNexusEndpointAsync(endpoint);
            }
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Workflow]
    public class CustomFuncWorkflow
    {
        private readonly Func<Task<int>> func;

        public CustomFuncWorkflow(Func<Task<int>> func) => this.func = func;

        [WorkflowRun]
        public Task<int> RunAsync() => func();
    }
}