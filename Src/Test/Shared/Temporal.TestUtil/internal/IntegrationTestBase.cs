using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Temporal.WorkflowClient;
using Xunit.Abstractions;

namespace Temporal.TestUtil
{
    public class IntegrationTestBase : TestBase
    {
        private const bool RedirectServerOutToCoutDefault = false;

        private readonly bool _redirectServerOutToCout;
        private ITemporalTestServerController _testServer = null;

        private readonly TestCaseContextMonikers _testCaseContextMonikers;

        public IntegrationTestBase(ITestOutputHelper cout, int temporalServicePort, TestTlsOptions testTlsOptions)
            : this(cout, RedirectServerOutToCoutDefault, temporalServicePort, testTlsOptions)
        {
        }

        public IntegrationTestBase(ITestOutputHelper cout, bool redirectServerOutToCout, int temporalServicePort, TestTlsOptions testTlsOptions)
            : base(cout)
        {
            _redirectServerOutToCout = redirectServerOutToCout;
            Port = temporalServicePort;
            TlsOptions = testTlsOptions;
            _testCaseContextMonikers = new TestCaseContextMonikers(System.DateTimeOffset.Now);
        }

        protected TestTlsOptions TlsOptions { get; }

        protected int Port { get; }

        internal virtual ITemporalTestServerController TestServer
        {
            get { return _testServer; }
        }

        internal virtual TestCaseContextMonikers TestCaseContextMonikers
        {
            get { return _testCaseContextMonikers; }
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            // In the future, when we will have other test servers (docker base, remote, ...), we will use some sort of
            // configuration mechanism to decide on the implementation chosen.

            _testServer = new TemporalLiteExeTestServerController(Cout, _redirectServerOutToCout);
            await _testServer.StartAsync(TlsOptions, Port);
        }

        public override async Task DisposeAsync()
        {
            ITemporalTestServerController testServer = Interlocked.Exchange(ref _testServer, null);
            if (testServer != null)
            {
                await testServer.ShutdownAsync();
            }

            await base.DisposeAsync();
        }

        protected TemporalClient CreateTemporalClient()
        {
            return TlsOptions switch
            {
                TestTlsOptions.None => new TemporalClient(),
                TestTlsOptions.Server => new TemporalClient(new TemporalClientConfiguration
                {
                    ServiceConnection = new TemporalClientConfiguration.Connection(ServerHost: "localhost",
                        ServerPort: Port,
                        IsTlsEnabled: true,
                        ClientIdentityCert: null,
                        SkipServerCertValidation: false,
                        ServerCertAuthority: TemporalClientConfiguration.TlsCertificate.FromPemFile(TestEnvironment.CaCertificatePath)),
                }),
                TestTlsOptions.Mutual => new TemporalClient(new TemporalClientConfiguration
                {
                    ServiceConnection = new TemporalClientConfiguration.Connection("localhost",
                        ServerPort: Port,
                    IsTlsEnabled: true,
                    ClientIdentityCert: TemporalClientConfiguration.TlsCertificate.FromPemFile(TestEnvironment.ClientCertificatePath,
                        TestEnvironment.ClientKeyPath),
                    SkipServerCertValidation: false,
                    ServerCertAuthority: TemporalClientConfiguration.TlsCertificate.FromPemFile(TestEnvironment.CaCertificatePath)),
                }),
                _ => throw new ArgumentException("Invalid value for TlsOptions", nameof(TlsOptions))
            };
        }

        protected string TestCaseWorkflowId([CallerMemberName] string testMethodName = null)
        {
            return TestCaseContextMonikers.ForWorkflowId(this, testMethodName);
        }

        protected string TestCaseTaskQueue([CallerMemberName] string testMethodName = null)
        {
            return TestCaseContextMonikers.ForTaskQueue(this, testMethodName);
        }
    }
}