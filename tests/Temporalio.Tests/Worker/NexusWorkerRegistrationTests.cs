namespace Temporalio.Tests.Worker;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Nexus;
using Temporalio.Worker;
using Xunit;

public class NexusWorkerRegistrationTests
{
    [NexusService]
    public interface IBadService
    {
        [NexusOperation]
        int DoSomething(string name);
    }

    [NexusServiceHandler(typeof(IBadService))]
    public class BadService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> DoSomething() =>
            throw new NotImplementedException();
    }

    [Fact]
    public void AddNexusService_BadService_FailsRegistration()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new BadService()));
        Assert.Equal("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Equal(
            "Expected return type of IOperationHandler<String, Int32>",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [NexusServiceHandler(typeof(NexusWorkerTests.IStringService))]
    public class TemporalOperationAttrBadReturnService
    {
        [TemporalOperation]
        public Task<string> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(input);
    }

    [Fact]
    public void AddNexusService_TemporalOperationBadReturnType_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrBadReturnService()));
        Assert.Contains("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Contains(
            "must return Task<TemporalOperationResult<String>>",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [NexusServiceHandler(typeof(NexusWorkerTests.IStringService))]
    public class TemporalOperationAttrBadParamsService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<string>> DoSomething(string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }

    [Fact]
    public void AddNexusService_TemporalOperationBadParams_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrBadParamsService()));
        Assert.Contains("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Contains(
            "must accept parameters (TemporalOperationStartContext, ITemporalNexusClient, String)",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

#pragma warning disable CA1052 // Intentionally non-static so registration reaches signature check
    [NexusServiceHandler(typeof(NexusWorkerTests.IStringService))]
    public class TemporalOperationAttrStaticService
    {
        [TemporalOperation]
        public static Task<TemporalOperationResult<string>> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }
#pragma warning restore CA1052

    [Fact]
    public void AddNexusService_TemporalOperationStaticMethod_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrStaticService()));
        Assert.Contains("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Contains(
            "must not be static",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [NexusServiceHandler(typeof(NexusWorkerTests.IGenericInputService))]
    public class TemporalOperationAttrGenericInputMismatchService
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<int>> Sum(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, List<string> values) =>
            Task.FromResult(TemporalOperationResult<int>.SyncResult(0));
    }

    [Fact]
    public void AddNexusService_TemporalOperationGenericInputMismatch_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrGenericInputMismatchService()));
        Assert.Contains("Failed obtaining operation handler from Sum", exc.Message);
        Assert.Contains(
            "must accept parameters",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [NexusServiceHandler(typeof(NexusWorkerTests.IStringService))]
    public class TemporalOperationAttrRawReturnService
    {
        [TemporalOperation]
        public Task<int> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(0);
    }

    [Fact]
    public void AddNexusService_TemporalOperationRawReturn_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrRawReturnService()));
        Assert.Contains("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Contains(
            "must return Task<TemporalOperationResult<String>>",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [NexusServiceHandler(typeof(NexusWorkerTests.IStringService))]
    public class TemporalOperationAttrDualAnnotationService
    {
        [TemporalOperation]
        [NexusOperationHandler]
        public Task<TemporalOperationResult<string>> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }

    [Fact]
    public void AddNexusService_TemporalOperationDualAnnotation_Throws()
    {
        // The built-in [NexusOperationHandler] path claims the method first, and it
        // rejects the signature (return type isn't IOperationHandler<,>).
        Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrDualAnnotationService()));
    }

    [NexusServiceHandler(typeof(NexusWorkerTests.IStringService))]
    public class TemporalOperationAttrPrivateService
    {
        [TemporalOperation]
        private Task<TemporalOperationResult<string>> DoSomething(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, string input) =>
            Task.FromResult(TemporalOperationResult<string>.SyncResult(input));
    }

    [Fact]
    public void AddNexusService_TemporalOperationPrivateMethod_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddNexusService(new TemporalOperationAttrPrivateService()));
        Assert.Contains("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Contains(
            "must be public",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }
}
