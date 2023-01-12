namespace Temporalio.Tests;

public class Program
{
    internal static bool InProc { get; private set; } = false;

    public static int Main(string[] args)
    {
        InProc = true;
        // Always put self assembly as first arg if "--help" isn't first arg
        if (args.Length != 1 || args[0] != "--help")
        {
            var newArgs = new string[args.Length + 1];
            newArgs[0] = typeof(Program).Assembly.Location;
            Array.Copy(args, 0, newArgs, 1, args.Length);
            args = newArgs;
        }
        return Xunit.ConsoleClient.Program.Main(args);
    }
}
