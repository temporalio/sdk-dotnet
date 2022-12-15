using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;

namespace Temporalio.Testing
{
    /// <summary>
    /// Workflow environment for testing with Temporal.
    /// </summary>
    /// <remarks>
    /// In modern versions of .NET, this implements <see cref="IAsyncDisposable" /> which means it
    /// can be used in an <c>await using</c> block.
    /// </remarks>
#if NETCOREAPP3_0_OR_GREATER
    public class WorkflowEnvironment : IAsyncDisposable
#else
    public class WorkflowEnvironment
#endif
    {
        /// <summary>
        /// Start a local test server with full Temporal capabilities but no time skipping.
        /// </summary>
        /// <param name="options">Options for the server.</param>
        /// <returns>The started environment.</returns>
        /// <remarks>
        /// By default this lazily downloads a server from the internet if not already present.
        /// </remarks>
        public static async Task<WorkflowEnvironment> StartLocalAsync(
            WorkflowEnvironmentStartLocalOptions? options = null
        )
        {
            options ??= new();
            var runtime = options.Runtime ?? Runtime.Default;
            var server = await Bridge.EphemeralServer.StartTemporaliteAsync(
                runtime.runtime,
                options
            );
            return await StartEphemeralAsync(server, options);
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
            WorkflowEnvironmentStartTimeSkippingOptions? options = null
        )
        {
            options ??= new();
            var runtime = options.Runtime ?? Runtime.Default;
            var server = await Bridge.EphemeralServer.StartTestServerAsync(
                runtime.runtime,
                options
            );
            return await StartEphemeralAsync(server, options);
        }

        private static async Task<WorkflowEnvironment> StartEphemeralAsync(
            Bridge.EphemeralServer server,
            TemporalClientConnectOptions options
        )
        {
            // Copy the options, replacing target
            options = (TemporalClientConnectOptions)options.Clone();
            options.TargetHost = server.Target;
            // If we can't connect, shutdown the server before returning
            try
            {
                return new WorkflowEnvironment.EphemeralServerBased(
                    await TemporalClient.ConnectAsync(options),
                    server
                );
            }
            catch (Exception ex)
            {
                try
                {
                    await server.ShutdownAsync();
                }
                catch (Exception ex2)
                {
                    throw new AggregateException(ex, ex2);
                }
                throw;
            }
        }

        /// <summary>
        /// Create a non-time-skipping workflow environment from an existing client.
        /// </summary>
        /// <param name="client">Client to use for this environment.</param>
        public WorkflowEnvironment(ITemporalClient client)
        {
            Client = client;
        }

        /// <summary>
        /// Gets the client for this workflow environment.
        /// </summary>
        public ITemporalClient Client { get; protected init; }

        /// <summary>
        /// Whether this environment supports time skipping.
        /// </summary>
        public virtual bool SupportsTimeSkipping => false;

        /// <summary>
        /// Sleep in this environment.
        /// </summary>
        /// <param name="millisecondsDelay">Number of milliseconds to sleep.</param>
        /// <param name="cancellationToken">If present, cancellation token for the sleep.</param>
        /// <remarks>
        /// In non-time-skipping environments, this is just
        /// <see cref="Task.Delay(int, CancellationToken)" />, but in time-skipping environments
        /// this may simply fast forward time.
        /// </remarks>
        public Task DelayAsync(int millisecondsDelay, CancellationToken? cancellationToken = null)
        {
            return DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
        }

        /// <summary>
        /// Sleep in this environment.
        /// </summary>
        /// <param name="delay">Amount of time to sleep.</param>
        /// <param name="cancellationToken">If present, cancellation token for the sleep.</param>
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
        /// <returns>The current time for the environment</returns>
        /// <remarks>
        /// For non-time-skipping environments this is just <see cref="DateTime.Now" />, but in
        /// time-skipping environments this may be a different time.
        /// </remarks>
        public virtual Task<DateTime> GetCurrentTimeAsync()
        {
            return Task.FromResult(DateTime.Now);
        }

        /// <summary>
        /// Disable automatic time skipping.
        /// </summary>
        /// <returns>The disposable that must be disposed to re-enable auto time skipping.</returns>
        /// <remarks>
        /// This has no effect if time skipping is already disabled (which is always the case in
        /// non-time-skipping environments).
        /// </remarks>
        public virtual IDisposable AutoTimeSkippingDisabled()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Shutdown this server.
        /// </summary>
        public virtual Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Shutdown and dispose this server.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await ShutdownAsync();
            GC.SuppressFinalize(this);
        }
#endif

        internal class EphemeralServerBased : WorkflowEnvironment
        {
            private readonly Bridge.EphemeralServer server;

            public EphemeralServerBased(TemporalClient client, Bridge.EphemeralServer server)
                : base(client)
            {
                this.server = server;
            }

            public override bool SupportsTimeSkipping => server.HasTestService;

            public async override Task DelayAsync(
                TimeSpan delay,
                CancellationToken? cancellationToken = null
            )
            {
                if (!SupportsTimeSkipping)
                {
                    await base.DelayAsync(delay, cancellationToken);
                }
                else
                {
                    // Unlock with sleep
                    var req = new Api.TestService.V1.SleepRequest
                    {
                        Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(delay)
                    };
                    await Client.Connection.TestService.UnlockTimeSkippingWithSleepAsync(
                        req,
                        new RpcOptions { CancellationToken = cancellationToken }
                    );
                }
            }

            public async override Task<DateTime> GetCurrentTimeAsync()
            {
                if (!SupportsTimeSkipping)
                {
                    return await base.GetCurrentTimeAsync();
                }
                var resp = await Client.Connection.TestService.GetCurrentTimeAsync(new());
                return resp.Time.ToDateTime();
            }

            public async override Task ShutdownAsync()
            {
                await server.ShutdownAsync();
            }
        }
    }
}
