namespace Temporalio.Tests;

using Temporalio.Common.EnvConfig;
using Temporalio.Testing;

// The SDK's WorkflowEnvironment only implements IAsyncDisposable on .NET Core, so tests that
// start their own environment use this wrapper to keep `await using` on every target.
#if NETFRAMEWORK
public sealed class AsyncDisposableWorkflowEnvironment : Temporalio.Testing.WorkflowEnvironment, IAsyncDisposable
#else
public sealed class AsyncDisposableWorkflowEnvironment : Temporalio.Testing.WorkflowEnvironment
#endif
{
    private readonly Temporalio.Testing.WorkflowEnvironment inner;

    private AsyncDisposableWorkflowEnvironment(Temporalio.Testing.WorkflowEnvironment inner)
        : base(inner.Client) => this.inner = inner;

    public override bool SupportsTimeSkipping => inner.SupportsTimeSkipping;

    public static new async Task<AsyncDisposableWorkflowEnvironment> CreateFromEnvConfigAsync(
        ClientEnvConfig.ProfileLoadOptions? profileLoadOptions = null) =>
        new(await Temporalio.Testing.WorkflowEnvironment.CreateFromEnvConfigAsync(profileLoadOptions));

    public static new async Task<AsyncDisposableWorkflowEnvironment> StartLocalAsync(
        WorkflowEnvironmentStartLocalOptions? options = null) =>
        new(await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(options));

    public static new async Task<AsyncDisposableWorkflowEnvironment> StartTimeSkippingAsync(
        WorkflowEnvironmentStartTimeSkippingOptions? options = null) =>
        new(await Temporalio.Testing.WorkflowEnvironment.StartTimeSkippingAsync(options));

    public override Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken = null) =>
        inner.DelayAsync(delay, cancellationToken);

    public override Task<DateTime> GetCurrentTimeAsync() => inner.GetCurrentTimeAsync();

    public override Task<T> WithAutoTimeSkippingDisabledAsync<T>(Func<Task<T>> func) =>
        inner.WithAutoTimeSkippingDisabledAsync(func);

    public override Task ShutdownAsync() => inner.ShutdownAsync();

#if NETFRAMEWORK
    public async ValueTask DisposeAsync() => await ShutdownAsync();
#endif
}
