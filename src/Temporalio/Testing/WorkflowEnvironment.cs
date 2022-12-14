using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;

namespace Temporalio.Testing
{
#if NETCOREAPP3_0_OR_GREATER
    public class WorkflowEnvironment : IAsyncDisposable
#else
    public class WorkflowEnvironment
#endif
    {
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
            // Cannot be lazy
            if (options.Lazy)
            {
                throw new InvalidOperationException(
                    "Workflow environment cannot have Lazy option as true"
                );
            }
            // Copy the options, replacing target
            options = new(options) { TargetHost = server.Target };
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

        public WorkflowEnvironment(TemporalClient client)
        {
            Client = client;
        }

        public TemporalClient Client { get; protected init; }

        public virtual bool SupportsTimeSkipping => false;

        public Task DelayAsync(int millisecondsDelay, CancellationToken? cancellationToken = null)
        {
            return DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
        }

        public virtual Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken = null)
        {
            if (cancellationToken == null)
            {
                return Task.Delay(delay);
            }
            return Task.Delay(delay, cancellationToken.Value);
        }

        public virtual Task<DateTime> GetCurrentTimeAsync()
        {
            return Task.FromResult(DateTime.Now);
        }

        public virtual IDisposable AutoTimeSkippingDisabled()
        {
            throw new NotImplementedException();
        }

        public virtual Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }

#if NETCOREAPP3_0_OR_GREATER
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
                        new(cancellationToken: cancellationToken)
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
