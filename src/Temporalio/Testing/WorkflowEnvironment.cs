using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Runtime;

namespace Temporalio.Testing
{
    /// <summary>
    /// Workflow environment for testing with Temporal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is NOT thread safe and not safe for use by concurrent tests. Time skipping is
    /// locked/unlocked at an environment level, so while reuse is supported, independent concurrent
    /// use can have side effects.
    /// </para>
    /// <para>
    /// In modern versions of .NET, this implements <c>IAsyncDisposable</c> which means it can be
    /// used in an <c>await using</c> block.
    /// </para>
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
        public WorkflowEnvironment(ITemporalClient client)
        {
            Client = client;
            Logger = client.Options.LoggerFactory.CreateLogger<WorkflowEnvironment>();
        }

        /// <summary>
        /// Gets the client for this workflow environment.
        /// </summary>
        public ITemporalClient Client { get; protected init; }

        /// <summary>
        /// Gets a value indicating whether this environment supports time skipping.
        /// </summary>
        public virtual bool SupportsTimeSkipping => false;

        /// <summary>
        /// Gets a logger for this environment.
        /// </summary>
        protected ILogger<WorkflowEnvironment> Logger { get; private init; }

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
            var server = await Bridge.EphemeralServer.StartDevServerAsync(
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

            // If they did not provide a binary to use, eagerly fail on musl since musl
            // time-skipping is not supported at this time. We can only do this check in .NET 5+,
            // otherwise we assume it is not musl for now since this is just a temporary situation.
            // TODO(cretz): Remove when musl-support for time-skipping test server is present.
#if NET5_0_OR_GREATER
            if (options.TestServer.ExistingPath == null &&
                System.Runtime.InteropServices.RuntimeInformation.Contains("-musl", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Time-skipping test server not currently supported in musl-based environments");
            }
#endif

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
        /// Run a function with automatic time skipping disabled.
        /// </summary>
        /// <param name="func">Function to run.</param>
        /// <remarks>
        /// This has no effect if time skipping is already disabled (which is always the case in
        /// non-time-skipping environments), so the function just runs.
        /// </remarks>
        public void WithAutoTimeSkippingDisabled(Action func) =>
            WithAutoTimeSkippingDisabled(() =>
            {
                func();
                return ValueTuple.Create();
            });

        /// <summary>
        /// Run a function with automatic time skipping disabled.
        /// </summary>
        /// <typeparam name="T">Type of result.</typeparam>
        /// <param name="func">Function to run.</param>
        /// <returns>Resulting value.</returns>
        /// <remarks>
        /// This has no effect if time skipping is already disabled (which is always the case in
        /// non-time-skipping environments), so the function just runs.
        /// </remarks>
        public T WithAutoTimeSkippingDisabled<T>(Func<T> func) =>
#pragma warning disable VSTHRD002 // We know we aren't deadlocking here
            WithAutoTimeSkippingDisabledAsync(() => Task.FromResult(func())).Result;
#pragma warning restore VSTHRD002

        /// <summary>
        /// Run a function with automatic time skipping disabled.
        /// </summary>
        /// <param name="func">Function to run.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// This has no effect if time skipping is already disabled (which is always the case in
        /// non-time-skipping environments), so the function just runs.
        /// </remarks>
        public Task WithAutoTimeSkippingDisabledAsync(Func<Task> func) =>
            WithAutoTimeSkippingDisabledAsync(async () =>
            {
                await func().ConfigureAwait(false);
                return ValueTuple.Create();
            });

        /// <summary>
        /// Run a function with automatic time skipping disabled.
        /// </summary>
        /// <typeparam name="T">Type of result.</typeparam>
        /// <param name="func">Function to run.</param>
        /// <returns>Task with resulting value.</returns>
        /// <remarks>
        /// This has no effect if time skipping is already disabled (which is always the case in
        /// non-time-skipping environments), so the function just runs.
        /// </remarks>
        public virtual Task<T> WithAutoTimeSkippingDisabledAsync<T>(Func<Task<T>> func) => func();

        /// <summary>
        /// Shutdown this server.
        /// </summary>
        /// <returns>Completion task.</returns>
        /// <remarks>
        /// This has no effect for workflow environments constructed with a simple client.
        /// </remarks>
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
            private bool autoTimeSkipping = true;

            /// <summary>
            /// Initializes a new instance of the <see cref="EphemeralServerBased"/> class.
            /// </summary>
            /// <param name="client">Client for the server.</param>
            /// <param name="server">The underlying bridge server.</param>
            public EphemeralServerBased(TemporalClient client, Bridge.EphemeralServer server)
                : base(client)
            {
                this.server = server;
                // Add time skipping interceptor
                Client = ClientWithTimeSkippingInterceptor(this, client);
            }

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

            /// <inheritdoc/>
            public override async Task<T> WithAutoTimeSkippingDisabledAsync<T>(Func<Task<T>> func)
            {
                var alreadyDisabled = autoTimeSkipping;
                autoTimeSkipping = false;
                try
                {
                    return await func().ConfigureAwait(false);
                }
                finally
                {
                    if (!alreadyDisabled)
                    {
                        autoTimeSkipping = true;
                    }
                }
            }

            /// <summary>
            /// Run the given function with time-skipping unlocked.
            /// </summary>
            /// <typeparam name="T">Return type.</typeparam>
            /// <param name="run">Function to run.</param>
            /// <returns>Task with result.</returns>
            public async Task<T> WithTimeSkippingUnlockedAsync<T>(Func<Task<T>> run)
            {
                // If disabled or not supported, no locking/unlocking, just run and return
                if (!SupportsTimeSkipping || !autoTimeSkipping)
                {
                    return await run().ConfigureAwait(false);
                }

                // Unlock to start time skipping, lock again to stop it
                await Client.Connection.TestService.UnlockTimeSkippingAsync(new()).ConfigureAwait(false);
                // We want to lock after user code and we want our lock call to throw only if the
                // user code didn't, otherwise just error
                var userCodeSucceeded = false;
                try
                {
                    var ret = await run().ConfigureAwait(false);
                    userCodeSucceeded = true;
                    // Attempt lock, throwing on failure
                    await Client.Connection.TestService.LockTimeSkippingAsync(new()).ConfigureAwait(false);
                    return ret;
                }
                catch (Exception) when (!userCodeSucceeded)
                {
                    // Attempt lock, swallowing failure
                    try
                    {
                        await Client.Connection.TestService.LockTimeSkippingAsync(new()).ConfigureAwait(false);
                    }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                    catch (Exception e)
#pragma warning restore CA1031
                    {
                        Logger.LogError(e, "Unable to re-lock time skipping");
                    }
                    throw;
                }
            }

            private static TemporalClient ClientWithTimeSkippingInterceptor(
                EphemeralServerBased env, TemporalClient orig)
            {
                var newInterceptors = new List<Client.Interceptors.IClientInterceptor>
                {
                    new TimeSkippingClientInterceptor(env),
                };
                if (orig.Options.Interceptors != null)
                {
                    newInterceptors.AddRange(orig.Options.Interceptors);
                }
                var newOptions = (TemporalClientOptions)orig.Options.Clone();
                newOptions.Interceptors = newInterceptors;
                return new(orig.Connection, newOptions);
            }
        }
    }
}
