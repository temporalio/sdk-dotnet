using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexusRpc.Handlers;
using Temporalio.Extensions.Hosting;
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
