namespace Temporalio.Tests;

using Xunit.Abstractions;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper output)
    {
        // Only set this if not in-proc
        if (!Program.InProc)
        {
            Console.SetOut(new ConsoleWriter(output));
        }
    }
}
