using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Extensions.Hosting;
using Temporalio.Nexus;
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

    [NexusService]
    public interface IIncrementService
    {
        [NexusOperation]
        int Increment(string input);
    }

    [NexusService]
    public interface IStringService
    {
        [NexusOperation]
        string DoSomething(string input);
    }

    [NexusService]
    public interface IAsyncSuffixService
    {
        [NexusOperation]
        string DoSomething(string input);

        [NexusOperation]
        string DoSomethingAsync(string input);
    }

    [NexusService]
    public interface IAsyncSuffixOnlyService
    {
        [NexusOperation]
        string DoSomethingAsync(string input);
    }

    // A [NexusOperationHandler] method whose name maps to no operation on the service interface.
    // Increment is a valid handler for the sole operation, so registration failure is isolated to
    // the unmatched NotAnOperation method rather than surfacing as a missing-handler error.
    [NexusServiceHandler(typeof(IIncrementService))]
    public class UnmatchedOperationHandlerNexusService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, int> Increment() =>
            throw new NotImplementedException();

        [NexusOperationHandler]
        public IOperationHandler<string, int> NotAnOperation() =>
            throw new NotImplementedException();
    }

    [NexusServiceHandler(typeof(IIncrementService))]
    public class AsyncOperationHandlerNexusService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, int> IncrementAsync() =>
            OperationHandler.Sync<string, int>((ctx, input) => 1);
    }

    [Fact]
    public void NexusWorkerService_OperationHandlerAsyncSuffix_FailsRegistration()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<AsyncOperationHandlerNexusService>();
        using var provider = services.BuildServiceProvider();

        var exc = Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue));
        Assert.Equal("Failed obtaining operation handler from IncrementAsync", exc.Message);
        Assert.Equal(
            "No matching NexusOperation on the service interface",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [NexusServiceHandler(typeof(IIncrementService))]
    public class AsyncTemporalOperationNexusService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<int>> IncrementAsync(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<int>.SyncResult(1));
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_Registers()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<AsyncTemporalOperationNexusService>();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue);
        Assert.Equal(
            "Increment",
            Assert.Single(Assert.Single(options.NexusServices).OperationHandlers).Key);
    }

    [NexusServiceHandler(typeof(IAsyncSuffixService))]
    public class TemporalOperationExactMatchService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<string>> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));

        [TemporalOperation]
        public Task<TemporalOperationResult<string>> DoSomethingAsync(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_MatchesExactFirst()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<TemporalOperationExactMatchService>();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue);
        var handlers = Assert.Single(options.NexusServices).OperationHandlers;
        Assert.Equal(2, handlers.Count);
        Assert.Contains("DoSomething", handlers.Keys);
        Assert.Contains("DoSomethingAsync", handlers.Keys);
    }

    [NexusServiceHandler(typeof(IStringService))]
    public class TemporalOperationDuplicateAsyncSuffixService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<string>> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));

        [TemporalOperation]
        public Task<TemporalOperationResult<string>> DoSomethingAsync(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_RejectsDuplicates()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<TemporalOperationDuplicateAsyncSuffixService>();
        using var provider = services.BuildServiceProvider();

        var exc = Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(taskQueue));
        Assert.Equal("Duplicate operation handler named DoSomething", exc.Message);
    }

    [NexusServiceHandler(typeof(IAsyncSuffixOnlyService))]
    public class TemporalOperationUnsuffixedService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<string>> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }

    [Fact]
    public void NexusWorkerService_TemporalOperationAsyncSuffix_DoesNotMatchInReverse()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddHostedTemporalWorker(taskQueue).
            AddScopedNexusService<TemporalOperationUnsuffixedService>();
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
