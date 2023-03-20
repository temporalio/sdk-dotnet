using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Runtime;

namespace Temporalio.Testing
{
    /// <summary>
    /// Workflow environment for testing with Temporal.
    /// </summary>
    /// <remarks>
    /// In modern versions of .NET, this implements <c>IAsyncDisposable</c> which means it can be
    /// used in an <c>await using</c> block.
    /// </remarks>
#if NETCOREAPP3_0_OR_GREATER
    public class WorkflowEnvironment : IAsyncDisposable
#else
    public class WorkflowEnvironment
#endif
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEnvironment"/> class for the given
        /// client that does not support time skipping.
        /// </summary>
        /// <param name="client">Client to use for this environment.</param>
        public WorkflowEnvironment(ITemporalClient client) => Client = client;

        /// <summary>
        /// Gets the client for this workflow environment.
        /// </summary>
        public ITemporalClient Client { get; protected init; }

        /// <summary>
        /// Gets a value indicating whether this environment supports time skipping.
        /// </summary>
        public virtual bool SupportsTimeSkipping => false;

        /// <summary>
        /// Start a local test server with full Temporal capabilities but no time skipping.
        /// </summary>
        /// <param name="options">Options for the server.</param>
        /// <returns>The started environment.</returns>
        /// <remarks>
        /// By default this lazily downloads a server from the internet if not already present.
        /// </remarks>
        public static async Task<WorkflowEnvironment> StartLocalAsync(
            WorkflowEnvironmentStartLocalOptions? options = null)
        {
            options ??= new();
            var runtime = options.Runtime ?? TemporalRuntime.Default;
            var server = await Bridge.EphemeralServer.StartTemporaliteAsync(
                runtime.Runtime,
                options).ConfigureAwait(false);
            return await StartEphemeralAsync(server, options).ConfigureAwait(false);
        }

        /// <summary>
        /// Start a local test server with time skipping but limited Temporal capabilities.
        /// </summary>
        /// <param name="options">Options for the server.</param>
        /// <returns>The started environment.</returns>
        /// <remarks>
        /// By default this lazily downloads a server from the internet if not already present.
        /// </remarks>
        public static async Task<WorkflowEnvironment> StartTimeSkippingAsync(
            WorkflowEnvironmentStartTimeSkippingOptions? options = null)
        {
            options ??= new();
            var runtime = options.Runtime ?? TemporalRuntime.Default;
            var server = await Bridge.EphemeralServer.StartTestServerAsync(
                runtime.Runtime,
                options).ConfigureAwait(false);
            return await StartEphemeralAsync(server, options).ConfigureAwait(false);
        }

        /// <summary>
        /// Sleep in this environment.
        /// </summary>
        /// <param name="millisecondsDelay">Number of milliseconds to sleep.</param>
        /// <param name="cancellationToken">If present, cancellation token for the sleep.</param>
        /// <returns>Completion task.</returns>
        /// <remarks>
        /// In non-time-skipping environments, this is just
        /// <see cref="Task.Delay(int, CancellationToken)" />, but in time-skipping environments
        /// this may simply fast forward time.
        /// </remarks>
        public Task DelayAsync(
            int millisecondsDelay, CancellationToken? cancellationToken = null) =>
            DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);

        /// <summary>
        /// Sleep in this environment.
        /// </summary>
        /// <param name="delay">Amount of time to sleep.</param>
        /// <param name="cancellationToken">If present, cancellation token for the sleep.</param>
        /// <returns>Completion task.</returns>
        /// <remarks>
        /// In non-time-skipping environments, this is just
        /// <see cref="Task.Delay(TimeSpan, CancellationToken)" />, but in time-skipping
        /// environments this may simply fast forward time.
        /// </remarks>
        public virtual Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken = null)
        {
            if (cancellationToken == null)
            {
                return Task.Delay(delay);
            }
            return Task.Delay(delay, cancellationToken.Value);
        }

        /// <summary>
        /// Get the current time for the environment.
        /// </summary>
        /// <returns>The current time for the environment.</returns>
        /// <remarks>
        /// For non-time-skipping environments this is just <see cref="DateTime.Now" />, but in
        /// time-skipping environments this may be a different time.
        /// </remarks>
        public virtual Task<DateTime> GetCurrentTimeAsync() => Task.FromResult(DateTime.Now);

        /// <summary>
        /// Disable automatic time skipping.
        /// </summary>
        /// <returns>The disposable that must be disposed to re-enable auto time skipping.</returns>
        /// <remarks>
        /// This has no effect if time skipping is already disabled (which is always the case in
        /// non-time-skipping environments).
        /// </remarks>
        public virtual IDisposable AutoTimeSkippingDisabled() =>
            throw new NotImplementedException();

        /// <summary>
        /// Shutdown this server.
        /// </summary>
        /// <returns>Completion task.</returns>
        public virtual Task ShutdownAsync() => Task.CompletedTask;

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Shutdown and dispose this server.
        /// </summary>
        /// <returns>Completion task.</returns>
        public async ValueTask DisposeAsync()
        {
            await ShutdownAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
#endif

        private static async Task<WorkflowEnvironment> StartEphemeralAsync(
            Bridge.EphemeralServer server,
            TemporalClientConnectOptions options)
        {
            // Copy the options, replacing target
            options = (TemporalClientConnectOptions)options.Clone();
            options.TargetHost = server.Target;
            // If we can't connect, shutdown the server before returning
            try
            {
                return new EphemeralServerBased(
                    await TemporalClient.ConnectAsync(options).ConfigureAwait(false),
                    server);
            }
            catch (Exception ex)
            {
                try
                {
                    await server.ShutdownAsync().ConfigureAwait(false);
                }
                catch (Exception ex2)
                {
                    throw new AggregateException(ex, ex2);
                }
                throw;
            }
        }

        /// <summary>
        /// Ephemeral server based workflow environment.
        /// </summary>
        internal class EphemeralServerBased : WorkflowEnvironment
        {
            private readonly Bridge.EphemeralServer server;

            /// <summary>
            /// Initializes a new instance of the <see cref="EphemeralServerBased"/> class.
            /// </summary>
            /// <param name="client">Client for the server.</param>
            /// <param name="server">The underlying bridge server.</param>
            public EphemeralServerBased(TemporalClient client, Bridge.EphemeralServer server)
                : base(client) =>
                this.server = server;

            /// <inheritdoc/>
            public override bool SupportsTimeSkipping => server.HasTestService;

            /// <inheritdoc/>
            public async override Task DelayAsync(
                TimeSpan delay,
                CancellationToken? cancellationToken = null)
            {
                if (!SupportsTimeSkipping)
                {
                    await base.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Unlock with sleep
                    var req = new Api.TestService.V1.SleepRequest
                    {
                        Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(delay),
                    };
                    await Client.Connection.TestService.UnlockTimeSkippingWithSleepAsync(
                        req,
                        new RpcOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);
                }
            }

            /// <inheritdoc/>
            public async override Task<DateTime> GetCurrentTimeAsync()
            {
                if (!SupportsTimeSkipping)
                {
                    return await base.GetCurrentTimeAsync().ConfigureAwait(false);
                }
                var resp = await Client.Connection.TestService.GetCurrentTimeAsync(
                    new()).ConfigureAwait(false);
                return resp.Time.ToDateTime();
            }

            /// <inheritdoc/>
            public override Task ShutdownAsync() => server.ShutdownAsync();
        }
    }
}
