namespace Temporalio.Tests;

using Xunit.Abstractions;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper output)
    {
        Console.SetOut(new ConsoleWriter(output));
    }
}
