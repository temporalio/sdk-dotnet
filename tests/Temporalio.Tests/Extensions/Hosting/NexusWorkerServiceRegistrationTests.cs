using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NexusRpc.Handlers;
using Temporalio.Extensions.Hosting;
using Temporalio.Nexus;
using Temporalio.Tests.Worker;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Extensions.Hosting;

public class NexusWorkerServiceRegistrationTests : WorkflowEnvironmentTestBase
{
    public NexusWorkerServiceRegistrationTests(
        ITestOutputHelper output,
        WorkflowEnvironment env)
        : base(output, env)
    {
    }

    // A [NexusOperationHandler] method whose name maps to no operation on the service interface.
    // Increment is a valid handler for the sole operation, so registration failure is isolated to
    // the unmatched NotAnOperation method rather than surfacing as a missing-handler error.
    [NexusServiceHandler(typeof(NexusWorkerServiceTests.ITestNexusService))]
    public class UnmatchedOperationHandlerNexusService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, int> Increment() =>
            throw new NotImplementedException();

        [NexusOperationHandler]
        public IOperationHandler<string, int> NotAnOperation() =>
            throw new NotImplementedException();
    }

    [NexusServiceHandler(typeof(NexusWorkerServiceTests.ITestNexusService))]
    public class AsyncTemporalOperationNexusService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<int>> IncrementAsync(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<int>.SyncResult(1));
    }

    [Fact]
    public async Task NexusWorkerService_TemporalOperationAsyncSuffix_Registers()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.
            AddSingleton(Client).
            AddHostedTemporalWorker($"tq-{Guid.NewGuid()}").
            AddScopedNexusService<AsyncTemporalOperationNexusService>();
        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_MatchesExactFirst()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<NexusWorkerRegistrationTests.TemporalOperationExactMatchService>();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue);
        Assert.Equal(
            new[] { "DoSomething", "DoSomethingAsync" },
            Assert.Single(options.NexusServices).OperationHandlers.Keys.OrderBy(name => name));
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_RejectsDuplicates()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<NexusWorkerRegistrationTests.TemporalOperationDuplicateAsyncSuffixService>();
        using var provider = services.BuildServiceProvider();

        var exc = Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue));
        Assert.Equal("Duplicate operation handler named DoSomething", exc.Message);
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_DoesNotMatchInReverse()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<NexusWorkerRegistrationTests.TemporalOperationUnsuffixedService>();
        using var provider = services.BuildServiceProvider();

        var exc = Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue));
        Assert.Contains("Missing handlers for defined operations", exc.Message);
        Assert.Contains("DoSomethingAsync", exc.Message);
    }

    // The hosting/DI registration path must reject a [NexusOperationHandler] method that maps to no
    // operation, matching NexusRpc's ServiceHandlerInstance.FromInstance and the non-DI
    // TemporalWorkerOptions.AddNexusService path (rather than silently skipping it). The worker
    // service resolves (and validates) its Nexus operations from options while starting, so the
    // failure surfaces from host startup.
    [Fact]
    public async Task NexusWorkerService_UnmatchedOperationHandler_FailsRegistration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.
            AddSingleton(Client).
            AddHostedTemporalWorker($"tq-{Guid.NewGuid()}").
            AddScopedNexusService<UnmatchedOperationHandlerNexusService>();
        using var host = builder.Build();

        var exc = await Assert.ThrowsAsync<ArgumentException>(() => host.StartAsync());
        Assert.Equal("Failed obtaining operation handler from NotAnOperation", exc.Message);
        Assert.Equal(
            "No matching NexusOperation on the service interface",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }
}
