namespace Temporalio.Tests;

using Xunit;
using Xunit.Abstractions;

[Collection("Environment")]
public abstract class WorkflowEnvironmentTestBase
{
    private readonly WorkflowEnvironment env;

    public WorkflowEnvironmentTestBase(ITestOutputHelper output, WorkflowEnvironment env)
    {
        Console.SetOut(new ConsoleWriter(output));
        this.env = env;
    }

    protected Temporalio.Client.TemporalClient Client => env.Client;
}
