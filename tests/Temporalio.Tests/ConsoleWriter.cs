namespace Temporalio.Tests;

using Xunit.Abstractions;

public class ConsoleWriter : StringWriter
{
    private ITestOutputHelper output;

    public ConsoleWriter(ITestOutputHelper output)
    {
        this.output = output;
    }

    public override void WriteLine(string? value)
    {
        output.WriteLine(value);
    }
}
