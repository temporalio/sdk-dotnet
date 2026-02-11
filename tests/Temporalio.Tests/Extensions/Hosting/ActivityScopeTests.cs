namespace Temporalio.Tests.Extensions.Hosting;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Extensions.Hosting;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class ActivityScopeTests : WorkflowEnvironmentTestBase
{
    public ActivityScopeTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    // Custom interceptor that fails when bad state is seen
    public class FailOnBadStateInterceptor : IWorkerInterceptor
    {
        private readonly IServiceProvider serviceProvider;

        public FailOnBadStateInterceptor(IServiceProvider serviceProvider) =>
            this.serviceProvider = serviceProvider;

        public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
            new ActivityInbound(serviceProvider, nextInterceptor);

        private class ActivityInbound : ActivityInboundInterceptor
        {
            private readonly IServiceProvider serviceProvider;

            public ActivityInbound(IServiceProvider serviceProvider, ActivityInboundInterceptor next)
                : base(next) => this.serviceProvider = serviceProvider;

            public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
            {
                // Make sure it's the expected activity
                if (input.Activity.Name != "SetSomeState")
                {
                    throw new InvalidOperationException("Unexpected activity");
                }

                // We want to control the instance so we have access to SomeState
                await using var scope = serviceProvider.CreateAsyncScope();
                ActivityScope.ServiceScope = scope;
                var instance = scope.ServiceProvider.GetRequiredService<MyActivities>();
                ActivityScope.ScopedInstance = instance;

                // Run the activity, but if SomeState is "should-fail", then raise
                var result = await base.ExecuteActivityAsync(input);
                if (instance.SomeState == "should-fail")
                {
                    throw new ApplicationFailureException("Intentional failure", nonRetryable: true);
                }
                return result;
            }
        }
    }

    public class MyActivities
    {
        public string SomeState { get; set; } = "<unset>";

        [Activity]
        public void SetSomeState(string someState)
        {
            Assert.NotNull(ActivityScope.ServiceScope);
            Assert.Same(ActivityScope.ScopedInstance, this);
            SomeState = someState;
        }
    }

    [Workflow]
    public class MyWorkflow
    {
        [WorkflowRun]
        public Task RunAsync(string someState) =>
            Workflow.ExecuteActivityAsync(
                (MyActivities acts) => acts.SetSomeState(someState),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [Fact]
    public async Task ActivityScope_CustomInstance_IsAccessible()
    {
        // Create the host
        using var loggerFactory = new TestUtils.LogCaptureFactory(NullLoggerFactory.Instance);
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var host = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            // Configure a client
            services.AddTemporalClient(
                clientTargetHost: Client.Connection.Options.TargetHost,
                clientNamespace: Client.Options.Namespace);

            // Add the rest of the services
            services.
                AddSingleton<ILoggerFactory>(loggerFactory).
                AddHostedTemporalWorker(taskQueue).
                AddScopedActivities<MyActivities>().
                AddWorkflow<MyWorkflow>().
                // Add the interceptor and give it the service provider
                ConfigureOptions().PostConfigure<IServiceProvider>((options, serviceProvider) =>
                    options.Interceptors = new List<IWorkerInterceptor>
                        { new FailOnBadStateInterceptor(serviceProvider) });
        }).Build();

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        await host.StartAsync(tokenSource.Token);
        try
        {
            // Execute the workflow successfully. confirming no scope leak
            Assert.Null(ActivityScope.ServiceScope);
            Assert.Null(ActivityScope.ScopedInstance);
            await Client.ExecuteWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync("should-succeed"),
                new($"wf-{Guid.NewGuid()}", taskQueue));
            Assert.Null(ActivityScope.ServiceScope);
            Assert.Null(ActivityScope.ScopedInstance);

            // Now execute a workflow with state that the interceptor will see set on the activity and
            // fail
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Client.ExecuteWorkflowAsync(
                    (MyWorkflow wf) => wf.RunAsync("should-fail"),
                    new($"wf-{Guid.NewGuid()}", taskQueue)));
            var exc2 = Assert.IsType<ActivityFailureException>(exc.InnerException);
            var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
            Assert.Equal("Intentional failure", exc3.Message);
        }
        finally
        {
            await host.StopAsync(tokenSource.Token);
        }
    }
}