namespace Temporalio.Tests;

using Xunit;
using Xunit.Abstractions;

[Collection("Environment")]
public abstract class WorkflowEnvironmentTestBase
{
    public WorkflowEnvironmentTestBase(ITestOutputHelper output, WorkflowEnvironment env)
    {
        Console.SetOut(new ConsoleWriter(output));
        Env = env;
    }

    protected WorkflowEnvironment Env { get; private init; }

    protected Temporalio.Client.ITemporalClient Client => Env.Client;
}
