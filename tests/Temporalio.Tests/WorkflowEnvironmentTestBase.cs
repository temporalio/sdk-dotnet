namespace Temporalio.Tests;

using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

[Collection("Environment")]
public abstract class WorkflowEnvironmentTestBase : TestBase
{
    protected WorkflowEnvironmentTestBase(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output)
    {
        Env = env;
        // We need to update the client logger with our factory
        var newOptions = (TemporalClientOptions)env.Client.Options.Clone();
        newOptions.LoggerFactory = LoggerFactory;
        Client = new TemporalClient(env.Client.Connection, newOptions);
    }

    protected WorkflowEnvironment Env { get; private init; }

    protected ITemporalClient Client { get; private init; }
}
