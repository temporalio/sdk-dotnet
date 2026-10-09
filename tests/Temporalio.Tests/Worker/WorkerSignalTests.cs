using System.Diagnostics;
using System.Runtime.InteropServices;
using Temporalio.Activities;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Worker;

public class WorkerSignalTests
{
    private readonly ITestOutputHelper output;

    public WorkerSignalTests(ITestOutputHelper output) => this.output = output;

    [SkippableTheory]
    [InlineData("SIGINT", 2, "signals")]
    [InlineData("SIGTERM", 15, "signals")]
    [InlineData("SIGINT", 2, "token")]
    [InlineData("SIGTERM", 15, "token")]
    [InlineData("SIGINT", 2, "both")]
    [InlineData("SIGTERM", 15, "both")]
    public async Task ExecuteAsync_Signal_DrainsActivity(
        string signal, int signalNumber, string mode)
    {
        Skip.If(OperatingSystem.IsWindows(), "Requires Unix signal delivery");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var startInfo = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("signal-worker");
        startInfo.ArgumentList.Add(mode);
        using var process = Process.Start(startInfo)!;
        var errors = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await ReadUntilAsync("READY");
            await SendSignalAsync();
            if (mode == "token")
            {
                await ReadUntilAsync("SIGNAL_RECEIVED");
                await process.StandardInput.WriteLineAsync("CANCEL");
                await process.StandardInput.FlushAsync(timeout.Token);
            }
            await ReadUntilAsync("SHUTTING_DOWN");
            Assert.False(process.HasExited);

            // Repeated signals must still allow the activity to drain during the grace period.
            await SendSignalAsync();
            await process.StandardInput.WriteLineAsync("RELEASE");
            await process.StandardInput.FlushAsync(timeout.Token);
            await ReadUntilAsync("STOPPED");
            await ReadUntilAsync("READY_FOR_EXIT");

            // No application or worker handler remains; default signal termination must be restored.
            await SendSignalAsync();
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(128 + signalNumber, process.ExitCode);
            output.WriteLine(await errors);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }

        async Task ReadUntilAsync(string expected)
        {
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
            {
                output.WriteLine(line);
                if (line == expected)
                {
                    return;
                }
            }
            Assert.Fail($"Worker exited before {expected}: {await errors}");
        }

        async Task SendSignalAsync()
        {
            using var sender = Process.Start(new ProcessStartInfo("/bin/kill")
            {
                ArgumentList = { "-s", signal, process.Id.ToString() },
                UseShellExecute = false,
            })!;
            await sender.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, sender.ExitCode);
        }
    }

    internal static async Task<int> RunWorkerAsync(string mode)
    {
        await using (var env = new WorkflowEnvironment())
        {
            await env.InitializeAsync();
            using var stoppingSource = new CancellationTokenSource();
            using var independentSource = new CancellationTokenSource();
            var started = new TaskCompletionSource<ActivityExecutionContext>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var signalReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            [Activity]
            async Task<string> DrainAsync()
            {
                var context = ActivityExecutionContext.Current;
                started.SetResult(context);
                try
                {
                    await Task.Delay(Timeout.Infinite, context.WorkerShutdownToken);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("SHUTTING_DOWN");
                }
                await release.Task;
                Assert.False(context.CancellationToken.IsCancellationRequested);
                return "drained";
            }

            using var worker = new TemporalWorker(
                env.Client,
                new TemporalWorkerOptions($"signal-{Guid.NewGuid()}")
                {
                    GracefulShutdownTimeout = TimeSpan.FromSeconds(30),
                }.AddActivity(DrainAsync));
            using var independentWorker = new TemporalWorker(
                env.Client,
                new TemporalWorkerOptions($"independent-{Guid.NewGuid()}").AddActivity(DrainAsync));
            var independentExecution = mode == "both" ? independentWorker.ExecuteAsync() :
                independentWorker.ExecuteAsync(independentSource.Token);
            using (var interrupt = mode == "token" ?
                PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal) : null)
            using (var terminate = mode == "token" ?
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal) : null)
            {
                var execution = mode == "token" ?
                    worker.ExecuteAsync(stoppingSource.Token) : worker.ExecuteAsync();
                var handle = await env.Client.StartActivityAsync(
                    ActivityDefinition.Create(DrainAsync).Name!,
                    Array.Empty<object?>(),
                    new($"activity-{Guid.NewGuid()}", worker.Options.TaskQueue!)
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                    });
                var context = await started.Task;
                Console.WriteLine("READY");
                if (mode == "token")
                {
                    await signalReceived.Task;
                    Console.WriteLine("SIGNAL_RECEIVED");
                    Assert.Equal("CANCEL", await Console.In.ReadLineAsync());
                    Assert.False(execution.IsCompleted);
                    Assert.False(context.WorkerShutdownToken.IsCancellationRequested);
                    await stoppingSource.CancelAsync();
                }

                Assert.Equal("RELEASE", await Console.In.ReadLineAsync());
                Assert.False(execution.IsCompleted);
                release.SetResult();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
                Assert.Equal("drained", await handle.GetResultAsync<string>());
                if (mode != "both")
                {
                    Assert.False(independentExecution.IsCompleted);
                    await independentSource.CancelAsync();
                }
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => independentExecution);
                Console.WriteLine("STOPPED");
            }

            void HandleSignal(PosixSignalContext context)
            {
                context.Cancel = true;
                signalReceived.TrySetResult();
            }
        }

        Console.WriteLine("READY_FOR_EXIT");
        await Task.Delay(Timeout.Infinite);
        return 0;
    }
}
