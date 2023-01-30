namespace Temporalio.Tests;

public static class TestUtils
{
    public static string CallerFilePath(
        [System.Runtime.CompilerServices.CallerFilePath] string? callerPath = null)
    {
        return callerPath ?? throw new ArgumentException("Unable to find caller path");
    }
}