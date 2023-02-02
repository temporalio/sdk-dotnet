namespace Temporalio.Tests;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper output)
    {
        if (Program.InProc)
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                builder.AddSimpleConsole().SetMinimumLevel(
                    Program.Verbose ? LogLevel.Trace : LogLevel.Debug));
        }
        else
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                builder.AddXUnit(output));
            // Only set this if not in-proc
            Console.SetOut(new ConsoleWriter(output));
        }
    }

    protected ILoggerFactory LoggerFactory { get; private init; }
}
