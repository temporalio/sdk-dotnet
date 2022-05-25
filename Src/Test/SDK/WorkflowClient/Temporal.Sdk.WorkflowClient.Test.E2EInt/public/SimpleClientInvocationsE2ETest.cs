using Xunit.Abstractions;
using Temporal.TestUtil;

namespace Temporal.Sdk.WorkflowClient.Test.E2EInt
{
    // ReSharper disable once UnusedType.Global
    public class SimpleClientInvocationsE2ETest : SimpleClientInvocationsE2ETestBase
    {
        public SimpleClientInvocationsE2ETest(ITestOutputHelper cout)
            : base(cout, TestTlsOptions.None, 7233)
        {
        }
    }
}